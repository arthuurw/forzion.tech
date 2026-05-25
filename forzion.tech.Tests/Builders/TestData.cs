namespace forzion.tech.Tests.Builders;

/// <summary>
/// Fonte deterministica de dados para os builders de teste. Seed fixo garante saida
/// reproduzivel — alinhado ao determinismo da Fase 1 (tempo via timestamp explicito).
/// </summary>
public static class TestData
{
    /// <summary>Seed fixo usado por toda construcao deterministica.</summary>
    public const int Seed = 20260524;

    /// <summary>Timestamp deterministico padrao para construcao de entidades.</summary>
    public static readonly DateTime Agora = new(2026, 5, 24, 12, 0, 0, DateTimeKind.Utc);

    private static readonly object Lock = new();
    private static int _counter;

    /// <summary>
    /// Guid deterministico a partir do seed + um contador monotonico. Reproduzivel
    /// dentro de um processo de teste sem colidir entre entidades distintas.
    /// </summary>
    public static Guid NextGuid()
    {
        lock (Lock)
        {
            var bytes = new byte[16];
            BitConverter.GetBytes(Seed).CopyTo(bytes, 0);
            BitConverter.GetBytes(++_counter).CopyTo(bytes, 4);
            return new Guid(bytes);
        }
    }
}
