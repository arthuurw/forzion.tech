using System.Text.Json;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Outbox;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.Outbox.Handlers;

public sealed class EmitirNfseEfeitoHandler(
    INotaFiscalRepository notaFiscalRepository,
    ITreinadorRepository treinadorRepository,
    IEmissorNfseService emissor,
    IOptions<NfseSettings> settings,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<EmitirNfseEfeitoHandler> logger) : IOutboxEfeitoHandler
{
    private readonly NfseSettings _settings = settings.Value;

    public string Tipo => "fx:emitir_nfse";

    public async Task ExecutarAsync(string payload, CancellationToken cancellationToken = default)
    {
        var data = JsonSerializer.Deserialize<EmitirNfsePayload>(payload)
            ?? throw new InvalidOperationException($"Payload inválido para {Tipo}: {payload}");

        var nota = await notaFiscalRepository.ObterPorIdAsync(data.NotaFiscalId, cancellationToken).ConfigureAwait(false);
        if (nota is null)
        {
            logger.LogWarning("NotaFiscal {NotaFiscalId} não encontrada para emissão. Efeito descartado.", data.NotaFiscalId);
            return;
        }

        if (nota.Status is not (NotaFiscalStatus.Pendente or NotaFiscalStatus.Erro))
        {
            logger.LogInformation("NotaFiscal {NotaFiscalId} em status {Status} não é emitível. Ignorado.", nota.Id, nota.Status);
            return;
        }

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        var treinador = await treinadorRepository.ObterPorIdAsync(nota.TreinadorId, cancellationToken).ConfigureAwait(false);
        if (treinador?.DadosFiscais is null)
        {
            logger.LogWarning("Treinador {TreinadorId} sem dados fiscais na emissão da NotaFiscal {NotaFiscalId}.", nota.TreinadorId, nota.Id);
            if (nota.Status == NotaFiscalStatus.Pendente)
            {
                nota.MarcarBloqueadaDadosFiscais(agora);
                await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        var codigoServico = nota.Tipo == TipoNotaFiscal.ComissaoMarketplace
            ? _settings.CodigoServicoComissao
            : _settings.CodigoServicoAssinatura;

        var input = new DpsInput(
            new DpsPrestador(_settings.CnpjPrestador, _settings.InscricaoMunicipal, _settings.CodigoMunicipioIbge, _settings.RegimeTributario),
            treinador.DadosFiscais,
            codigoServico,
            _settings.AliquotaIss,
            nota.Valor,
            nota.CompetenciaInicio ?? DateOnly.FromDateTime(nota.CreatedAt),
            nota.NumeroDps ?? nota.NumeroDpsEstavel());

        var resultado = await emissor.EmitirAsync(input, cancellationToken).ConfigureAwait(false);

        Result transicao;
        if (resultado.Sucesso)
        {
            transicao = nota.MarcarEmitida(
                resultado.ChaveAcesso ?? string.Empty,
                resultado.NumeroNfse ?? string.Empty,
                resultado.DataEmissao ?? agora,
                resultado.DanfseRef,
                agora);
        }
        else
        {
            transicao = nota.MarcarErro(
                resultado.CodigoErro ?? "DESCONHECIDO",
                resultado.MotivoErro ?? "Rejeição sem motivo informado pelo provedor.",
                agora);
        }

        if (transicao.IsFailure)
        {
            logger.LogWarning("Transição inválida da NotaFiscal {NotaFiscalId}: {Erro}.", nota.Id, transicao.Error!.Message);
            return;
        }

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
