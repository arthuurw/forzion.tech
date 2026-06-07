using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Events;

namespace forzion.tech.Application.UseCases.Pagamentos.PreAvisoRenovacao;

public class DespacharPreAvisosTreinadorHandler(
    IAssinaturaTreinadorRepository assinaturaRepository,
    IDomainEventDispatcher eventDispatcher,
    TimeProvider timeProvider)
    : DespacharPreAvisosHandlerBase<AssinaturaTreinador>(eventDispatcher, timeProvider)
{
    protected override Task<IReadOnlyList<AssinaturaTreinador>> ListarAsync(
        DateTime inicio, DateTime fim, CancellationToken cancellationToken) =>
        assinaturaRepository.ListarParaPreAvisoAsync(inicio, fim, cancellationToken);

    protected override IDomainEvent CriarEvento(AssinaturaTreinador a, DateTime agora) =>
        new CobrancaProximaTreinadorEvent(a.Id, a.TreinadorId, a.Valor, a.DataProximaCobranca, agora);
}
