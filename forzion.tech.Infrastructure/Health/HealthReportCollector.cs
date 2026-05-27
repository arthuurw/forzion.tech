using System.Reflection;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
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
    TimeProvider timeProvider,
    ITreinadorRepository treinadorRepository,
    IAlunoRepository alunoRepository,
    IContaRepository contaRepository,
    IPagamentoRepository pagamentoRepository,
    IAssinaturaAlunoRepository assinaturaRepository,
    IEmailDeliveryLogRepository emailDeliveryLogRepository,
    IErrorLogRepository errorLogRepository) : IHealthReportCollector
{
    private const int MaxAmostrasErro = 10;

    public async Task<HealthReport> ColetarAsync(HealthReportConfig config, CancellationToken cancellationToken = default)
    {
        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var bancoAcessivel = await PingBancoAsync(cancellationToken).ConfigureAwait(false);

        var liveness = config.IncluirLiveness ? MontarLiveness(bancoAcessivel) : null;
        var kpis = config.IncluirKpis ? await MontarKpisAsync(agora, cancellationToken).ConfigureAwait(false) : null;
        var entregabilidade = config.IncluirEntregabilidade
            ? await MontarEntregabilidadeAsync(agora, cancellationToken).ConfigureAwait(false)
            : null;
        var erros = config.IncluirErros ? await MontarErrosAsync(agora, cancellationToken).ConfigureAwait(false) : null;

        return new HealthReport
        {
            Ambiente = ObterAmbiente(),
            CapturadoEm = agora,
            StatusGeral = DerivarStatus(bancoAcessivel, erros),
            Liveness = liveness,
            Kpis = kpis,
            Entregabilidade = entregabilidade,
            Erros = erros
        };
    }

    private async Task<ErrosSecao> MontarErrosAsync(DateTime agora, CancellationToken cancellationToken)
    {
        var desde = agora.AddHours(-24);
        var total = await errorLogRepository.ContarDesdeAsync(desde, cancellationToken).ConfigureAwait(false);
        var amostras = await errorLogRepository.ListarDesdeAsync(desde, MaxAmostrasErro, cancellationToken).ConfigureAwait(false);

        return new ErrosSecao
        {
            Total = total,
            Amostras = amostras
                .Select(e => new ErroAmostra
                {
                    OcorridoEm = e.OcorridoEm,
                    Nivel = e.Nivel,
                    Origem = e.Origem,
                    Mensagem = e.Mensagem
                })
                .ToList()
        };
    }

    private async Task<EntregabilidadeSecao> MontarEntregabilidadeAsync(DateTime agora, CancellationToken cancellationToken)
    {
        var porEvento = await emailDeliveryLogRepository
            .ContarPorEventoDesdeAsync(agora.AddHours(-24), cancellationToken)
            .ConfigureAwait(false);

        int Contar(params string[] eventos) =>
            porEvento
                .Where(kv => eventos.Any(e => string.Equals(kv.Key, e, StringComparison.OrdinalIgnoreCase)))
                .Sum(kv => kv.Value);

        return new EntregabilidadeSecao
        {
            Total = porEvento.Values.Sum(),
            Entregues = Contar("email.delivered"),
            Bounces = Contar("email.bounced"),
            Spam = Contar("email.complained", "email.spam_complaint")
        };
    }

    private async Task<KpisSecao> MontarKpisAsync(DateTime agora, CancellationToken cancellationToken)
    {
        var desde24h = agora.AddHours(-24);

        return new KpisSecao
        {
            TreinadoresAtivos = await treinadorRepository.ContarPorStatusAsync(TreinadorStatus.Ativo, cancellationToken).ConfigureAwait(false),
            AlunosAtivos = await alunoRepository.ContarPorStatusAsync(AlunoStatus.Ativo, cancellationToken).ConfigureAwait(false),
            NovasContas24h = await contaRepository.ContarCriadasDesdeAsync(desde24h, cancellationToken).ConfigureAwait(false),
            PagamentosPendentes = await pagamentoRepository.ContarPorStatusAsync(PagamentoStatus.Pendente, cancellationToken).ConfigureAwait(false),
            PagamentosFalhos = await pagamentoRepository.ContarPorStatusAsync(PagamentoStatus.Falhou, cancellationToken).ConfigureAwait(false),
            AssinaturasAtivas = await assinaturaRepository.ContarPorStatusAsync(AssinaturaAlunoStatus.Ativa, cancellationToken).ConfigureAwait(false)
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

    private static StatusSaude DerivarStatus(bool bancoAcessivel, ErrosSecao? erros)
    {
        if (!bancoAcessivel)
            return StatusSaude.Falha;

        return erros is { Total: > 0 } ? StatusSaude.Degradado : StatusSaude.Ok;
    }

    private static string? ExtrairCommit(Assembly asm)
    {
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrEmpty(info))
            return null;

        var idx = info.IndexOf('+');
        return idx >= 0 && idx < info.Length - 1 ? info[(idx + 1)..] : null;
    }
}
