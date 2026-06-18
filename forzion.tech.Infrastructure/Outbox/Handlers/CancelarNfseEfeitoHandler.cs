using System.Text.Json;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Outbox;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.Outbox.Handlers;

public sealed class CancelarNfseEfeitoHandler(
    INotaFiscalRepository notaFiscalRepository,
    IEmissorNfseService emissor,
    IOptions<NfseSettings> settings,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<CancelarNfseEfeitoHandler> logger) : IOutboxEfeitoHandler
{
    private readonly NfseSettings _settings = settings.Value;

    public string Tipo => "fx:cancelar_nfse";

    public async Task ExecutarAsync(string payload, CancellationToken cancellationToken = default)
    {
        var data = JsonSerializer.Deserialize<CancelarNfsePayload>(payload)
            ?? throw new InvalidOperationException($"Payload inválido para {Tipo}: {payload}");

        var nota = await notaFiscalRepository.ObterPorIdAsync(data.NotaFiscalId, cancellationToken).ConfigureAwait(false);
        if (nota is null)
        {
            logger.LogWarning("NotaFiscal {NotaFiscalId} não encontrada para cancelamento. Efeito descartado.", data.NotaFiscalId);
            return;
        }

        if (nota.Status != NotaFiscalStatus.CancelamentoSolicitado)
        {
            logger.LogInformation("NotaFiscal {NotaFiscalId} em status {Status}; cancelamento não aplicável.", nota.Id, nota.Status);
            return;
        }

        if (string.IsNullOrWhiteSpace(nota.ChaveAcesso))
        {
            logger.LogWarning("NotaFiscal {NotaFiscalId} sem chave de acesso; cancelamento impossível.", nota.Id);
            return;
        }

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        if (PrazoExpirado(nota.DataEmissao, agora))
        {
            MarcarExpiradoComAlerta(nota, agora, "prazo de cancelamento expirado antes da transmissão");
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var resultado = await emissor.CancelarAsync(nota.ChaveAcesso, data.Motivo, cancellationToken).ConfigureAwait(false);

        if (!resultado.Sucesso)
        {
            MarcarExpiradoComAlerta(nota, agora, $"rejeição do provedor {resultado.CodigoErro}: {resultado.MotivoErro}");
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var transicao = nota.MarcarCancelada(agora);
        if (transicao.IsFailure)
        {
            logger.LogWarning("Transição de cancelamento inválida da NotaFiscal {NotaFiscalId}: {Erro}.", nota.Id, transicao.Error!.Message);
            return;
        }

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private bool PrazoExpirado(DateTime? dataEmissao, DateTime agora) =>
        dataEmissao is { } emissao && agora - emissao > TimeSpan.FromDays(_settings.PrazoCancelamentoDias);

    private void MarcarExpiradoComAlerta(NotaFiscal nota, DateTime agora, string motivo)
    {
        nota.MarcarCancelamentoExpirado(agora);
        logger.LogCritical(
            "Cancelamento da NotaFiscal {NotaFiscalId} (treinador {TreinadorId}) não concluído: {Motivo}. Ajuste fiscal manual necessário.",
            nota.Id, nota.TreinadorId, motivo);
    }
}
