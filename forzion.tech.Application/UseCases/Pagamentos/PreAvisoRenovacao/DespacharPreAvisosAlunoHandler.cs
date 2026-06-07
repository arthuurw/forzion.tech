using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Events;

namespace forzion.tech.Application.UseCases.Pagamentos.PreAvisoRenovacao;

public class DespacharPreAvisosAlunoHandler(
    IAssinaturaAlunoRepository assinaturaRepository,
    IDomainEventDispatcher eventDispatcher,
    TimeProvider timeProvider)
{
    public const int DiasAntecedencia = 3;

    public virtual async Task<int> HandleAsync(CancellationToken cancellationToken = default)
    {
        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var inicio = agora.Date.AddDays(DiasAntecedencia);
        var fim = inicio.AddDays(1);

        var assinaturas = await assinaturaRepository
            .ListarParaPreAvisoAsync(inicio, fim, cancellationToken).ConfigureAwait(false);

        var eventos = assinaturas
            .Select(a => (IDomainEvent)new CobrancaProximaAlunoEvent(
                a.Id, a.AlunoId, a.TreinadorId, a.Valor, a.DataProximaCobranca, agora))
            .ToList();

        if (eventos.Count > 0)
            await eventDispatcher.DispatchAsync(eventos, cancellationToken).ConfigureAwait(false);

        return eventos.Count;
    }
}
