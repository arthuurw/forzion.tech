using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.Notifications.Email;

public sealed class LimiteAlunosEmailSender(
    ITreinadorRepository treinadorRepository,
    IContaRepository contaRepository,
    IEmailService emailService,
    IOptions<AppSettings> appSettings,
    ILogger<LimiteAlunosEmailSender> logger) : ILimiteAlunosEmailSender
{
    public Task EnviarInicioAsync(Guid treinadorId, int excedente, DateTime dataLimite, CancellationToken cancellationToken = default) =>
        EnviarAsync(treinadorId,
            "Você excedeu o limite de alunos do seu plano — forzion.tech",
            (nome, linkPortal) => EmailTemplates.LimiteAlunosExcedido(nome, excedente, dataLimite, linkPortal),
            cancellationToken);

    public Task EnviarLembreteAsync(Guid treinadorId, int excedente, DateTime dataLimite, CancellationToken cancellationToken = default) =>
        EnviarAsync(treinadorId,
            "Lembrete — regularize o limite de alunos — forzion.tech",
            (nome, linkPortal) => EmailTemplates.LimiteAlunosLembrete(nome, excedente, dataLimite, linkPortal),
            cancellationToken);

    public Task EnviarAplicadoAsync(Guid treinadorId, int quantidadeDesativada, CancellationToken cancellationToken = default) =>
        EnviarAsync(treinadorId,
            "Ajuste aplicado — limite de alunos do seu plano — forzion.tech",
            (nome, linkPortal) => EmailTemplates.LimiteAlunosAplicado(nome, quantidadeDesativada, linkPortal),
            cancellationToken);

    private async Task EnviarAsync(
        Guid treinadorId,
        string assunto,
        Func<string, string, string> montarCorpo,
        CancellationToken cancellationToken)
    {
        if (!emailService.Habilitado) return;

        var treinador = await treinadorRepository.ObterPorIdAsync(treinadorId, cancellationToken).ConfigureAwait(false);
        if (treinador is null)
        {
            logger.LogWarning("LimiteAlunosEmailSender: treinador {Id} não encontrado.", treinadorId);
            return;
        }

        var conta = await contaRepository.ObterPorIdAsync(treinador.ContaId, cancellationToken).ConfigureAwait(false);
        if (conta is null)
        {
            logger.LogWarning("LimiteAlunosEmailSender: conta {Id} do treinador não encontrada.", treinador.ContaId);
            return;
        }

        var linkPortal = $"{appSettings.Value.FrontendBaseUrl}/treinador/plano";

        await emailService.EnviarAsync(
            conta.Email.Value,
            assunto,
            montarCorpo(treinador.Nome, linkPortal),
            cancellationToken).ConfigureAwait(false);
    }
}
