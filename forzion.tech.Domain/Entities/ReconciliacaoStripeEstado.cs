namespace forzion.tech.Domain.Entities;

public class ReconciliacaoStripeEstado
{
    public Guid Id { get; private set; }
    public DateTime UltimoEventoReconciliadoUtc { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private ReconciliacaoStripeEstado() { }

    public static ReconciliacaoStripeEstado Criar(DateTime ultimoEventoReconciliadoUtc, DateTime agora) => new()
    {
        Id = Guid.NewGuid(),
        UltimoEventoReconciliadoUtc = ultimoEventoReconciliadoUtc,
        CreatedAt = agora,
    };

    public void AvancarCursor(DateTime ateUtc, DateTime agora)
    {
        if (ateUtc <= UltimoEventoReconciliadoUtc)
            return;

        UltimoEventoReconciliadoUtc = ateUtc;
        UpdatedAt = agora;
    }
}
