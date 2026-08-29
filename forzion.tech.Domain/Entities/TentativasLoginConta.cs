namespace forzion.tech.Domain.Entities;

public class TentativasLoginConta
{
    public const int DelayMaximoSegundos = 30;
    private static readonly TimeSpan DelayBase = TimeSpan.FromSeconds(1);

    public Guid Id { get; private set; }
    public Guid ContaId { get; private set; }
    public int Tentativas { get; private set; }
    public DateTime AtualizadoEm { get; private set; }

    private TentativasLoginConta() { }

    public static TentativasLoginConta Criar(Guid contaId, DateTime agora) => new()
    {
        Id = Guid.NewGuid(),
        ContaId = contaId,
        Tentativas = 0,
        AtualizadoEm = agora,
    };

    public void RegistrarFalha(DateTime agora)
    {
        Tentativas++;
        AtualizadoEm = agora;
    }

    public void Zerar(DateTime agora)
    {
        Tentativas = 0;
        AtualizadoEm = agora;
    }

    public TimeSpan DelayAtual() => CalcularDelay(Tentativas);

    public static TimeSpan CalcularDelay(int tentativas)
    {
        if (tentativas <= 0)
            return TimeSpan.Zero;

        var segundos = Math.Min(DelayMaximoSegundos, DelayBase.TotalSeconds * Math.Pow(2, tentativas - 1));
        return TimeSpan.FromSeconds(segundos);
    }
}
