using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace forzion.tech.Infrastructure.Notifications.Email;

public sealed class LimiteAlunosEmailSender(
    IContaRepository contaRepository,
    IEmailService emailService,
    IOptions<AppSettings> appSettings,
    ILogger<LimiteAlunosEmailSender> logger) : ILimiteAlunosEmailSender
{
    public Task EnviarInicioAsync(Guid contaId, string nomeTreinador, int excedente, DateTime dataLimite, CancellationToken cancellationToken = default) =>
        EnviarAsync(contaId, nomeTreinador,
            "Você excedeu o limite de alunos do seu plano — forzion.tech",
            (nome, linkPortal) => EmailTemplates.LimiteAlunosExcedido(nome, excedente, dataLimite, linkPortal),
            cancellationToken);

    public Task EnviarLembreteAsync(Guid contaId, string nomeTreinador, int excedente, DateTime dataLimite, CancellationToken cancellationToken = default) =>
        EnviarAsync(contaId, nomeTreinador,
            "Lembrete — regularize o limite de alunos — forzion.tech",
            (nome, linkPortal) => EmailTemplates.LimiteAlunosLembrete(nome, excedente, dataLimite, linkPortal),
            cancellationToken);

    public Task EnviarAplicadoAsync(Guid contaId, string nomeTreinador, int quantidadeDesativada, CancellationToken cancellationToken = default) =>
        EnviarAsync(contaId, nomeTreinador,
            "Ajuste aplicado — limite de alunos do seu plano — forzion.tech",
            (nome, linkPortal) => EmailTemplates.LimiteAlunosAplicado(nome, quantidadeDesativada, linkPortal),
            cancellationToken);

    private async Task EnviarAsync(
        Guid contaId,
        string nomeTreinador,
        string assunto,
        Func<string, string, string> montarCorpo,
        CancellationToken cancellationToken)
    {
        if (!emailService.Habilitado) return;

        var conta = await contaRepository.ObterPorIdAsync(contaId, cancellationToken).ConfigureAwait(false);
        if (conta is null)
        {
            logger.LogWarning("LimiteAlunosEmailSender: conta {Id} do treinador não encontrada.", contaId);
            return;
        }

        var linkPortal = $"{appSettings.Value.FrontendBaseUrl}/treinador/plano";

        await emailService.EnviarAsync(
            conta.Email.Value,
            assunto,
            montarCorpo(nomeTreinador, linkPortal),
            cancellationToken).ConfigureAwait(false);
    }
}
