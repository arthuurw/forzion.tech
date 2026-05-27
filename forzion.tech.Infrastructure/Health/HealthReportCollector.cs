using System.Reflection;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.UseCases.Admin.HealthReport;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace forzion.tech.Infrastructure.Health;

public class HealthReportCollector(
    AppDbContext context,
    IEmailService emailService,
    IConfiguration configuration,
    TimeProvider timeProvider) : IHealthReportCollector
{
    public async Task<HealthReport> ColetarAsync(HealthReportConfig config, CancellationToken cancellationToken = default)
    {
        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var bancoAcessivel = await PingBancoAsync(cancellationToken).ConfigureAwait(false);

        var liveness = config.IncluirLiveness ? MontarLiveness(bancoAcessivel) : null;

        return new HealthReport
        {
            Ambiente = ObterAmbiente(),
            CapturadoEm = agora,
            StatusGeral = DerivarStatus(bancoAcessivel),
            Liveness = liveness
        };
    }

    private async Task<bool> PingBancoAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await context.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
    }

    private LivenessSecao MontarLiveness(bool bancoAcessivel)
    {
        var asm = Assembly.GetEntryAssembly() ?? typeof(HealthReportCollector).Assembly;
        var versao = asm.GetName().Version?.ToString();
        var commit = ExtrairCommit(asm);

        return new LivenessSecao
        {
            BancoAcessivel = bancoAcessivel,
            EmailHabilitado = emailService.Habilitado,
            StripeConfigurado = !string.IsNullOrWhiteSpace(configuration["Stripe:SecretKey"]),
            WhatsAppConfigurado = !string.IsNullOrWhiteSpace(configuration["WhatsApp:PhoneNumberId"])
                && !string.IsNullOrWhiteSpace(configuration["WhatsApp:AccessToken"]),
            Versao = versao,
            Commit = commit
        };
    }

    private string ObterAmbiente() =>
        configuration["ASPNETCORE_ENVIRONMENT"] ?? "Unknown";

    private static StatusSaude DerivarStatus(bool bancoAcessivel) =>
        bancoAcessivel ? StatusSaude.Ok : StatusSaude.Falha;

    private static string? ExtrairCommit(Assembly asm)
    {
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrEmpty(info))
            return null;

        var idx = info.IndexOf('+');
        return idx >= 0 && idx < info.Length - 1 ? info[(idx + 1)..] : null;
    }
}
