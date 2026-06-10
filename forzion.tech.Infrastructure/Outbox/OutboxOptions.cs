namespace forzion.tech.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    public int MaxTentativas { get; set; } = 5;
    public TimeSpan BackoffBase { get; set; } = TimeSpan.FromMinutes(1);
    public int LotePorCiclo { get; set; } = 20;
    public TimeSpan IntervaloPolling { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan RetencaoConcluidos { get; set; } = TimeSpan.FromDays(7);
    public TimeSpan IntervaloLimpeza { get; set; } = TimeSpan.FromHours(1);
}
