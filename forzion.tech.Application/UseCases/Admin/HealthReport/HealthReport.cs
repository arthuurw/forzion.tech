using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Admin.HealthReport;

public sealed record HealthReport
{
    public required string Ambiente { get; init; }
    public required DateTime CapturadoEm { get; init; }
    public required StatusSaude StatusGeral { get; init; }
    public LivenessSecao? Liveness { get; init; }
    public KpisSecao? Kpis { get; init; }
    public EntregabilidadeSecao? Entregabilidade { get; init; }
    public ErrosSecao? Erros { get; init; }
}

public sealed record LivenessSecao
{
    public required bool BancoAcessivel { get; init; }
    public required bool EmailHabilitado { get; init; }
    public required bool StripeConfigurado { get; init; }
    public required bool WhatsAppConfigurado { get; init; }
    public string? Versao { get; init; }
    public string? Commit { get; init; }
    public DateTime? UltimoDeployEm { get; init; }
}

public sealed record KpisSecao
{
    public required int TreinadoresAtivos { get; init; }
    public required int AlunosAtivos { get; init; }
    public required int NovasContas24h { get; init; }
    public required int PagamentosPendentes { get; init; }
    public required int PagamentosFalhos { get; init; }
    public required int AssinaturasAtivas { get; init; }
}

public sealed record EntregabilidadeSecao
{
    // Total de eventos de entrega rastreados na janela. Resend não emite "sent",
    // então só há feedback de entregues/bounce/spam (ver ProcessarWebhookResendHandler).
    public required int Total { get; init; }
    public required int Entregues { get; init; }
    public required int Bounces { get; init; }
    public required int Spam { get; init; }
}

public sealed record ErrosSecao
{
    public required int Total { get; init; }
    public required IReadOnlyList<ErroAmostra> Amostras { get; init; }
}

public sealed record ErroAmostra
{
    public required DateTime OcorridoEm { get; init; }
    public required string Nivel { get; init; }
    public required string Origem { get; init; }
    public required string Mensagem { get; init; }
}
