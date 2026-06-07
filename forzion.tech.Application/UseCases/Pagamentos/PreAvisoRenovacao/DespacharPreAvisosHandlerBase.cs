using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Events;

namespace forzion.tech.Application.UseCases.Pagamentos.PreAvisoRenovacao;

// Idempotência: a janela diária do cron limita o pré-aviso a 1× por DataProximaCobranca.
// Re-dispatch manual no mesmo dia reenviaria o e-mail (resíduo aceito — Resend não expõe
// header de idempotência via IEmailService sem alterar a assinatura em ~30 chamadores).
public abstract class DespacharPreAvisosHandlerBase<TAssinatura>(
    IDomainEventDispatcher eventDispatcher,
    TimeProvider timeProvider)
{
    public const int DiasAntecedencia = 3;

    protected abstract Task<IReadOnlyList<TAssinatura>> ListarAsync(
        DateTime inicio, DateTime fim, CancellationToken cancellationToken);

    protected abstract IDomainEvent CriarEvento(TAssinatura assinatura, DateTime agora);

    public virtual async Task<int> HandleAsync(CancellationToken cancellationToken = default)
    {
        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var inicio = agora.Date.AddDays(DiasAntecedencia);
        var fim = inicio.AddDays(1);

        var assinaturas = await ListarAsync(inicio, fim, cancellationToken).ConfigureAwait(false);

        var eventos = assinaturas.Select(a => CriarEvento(a, agora)).ToList();

        if (eventos.Count > 0)
            await eventDispatcher.DispatchAsync(eventos, cancellationToken).ConfigureAwait(false);

        return eventos.Count;
    }
}
