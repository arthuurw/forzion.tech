using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Events;

namespace forzion.tech.Application.UseCases.Pagamentos.PreAvisoRenovacao;

public class DespacharPreAvisosAlunoHandler(
    IAssinaturaAlunoRepository assinaturaRepository,
    IDomainEventDispatcher eventDispatcher,
    TimeProvider timeProvider)
    : DespacharPreAvisosHandlerBase<AssinaturaAluno>(eventDispatcher, timeProvider)
{
    protected override Task<IReadOnlyList<AssinaturaAluno>> ListarAsync(
        DateTime inicio, DateTime fim, CancellationToken cancellationToken) =>
        assinaturaRepository.ListarParaPreAvisoAsync(inicio, fim, cancellationToken);

    protected override IDomainEvent CriarEvento(AssinaturaAluno a, DateTime agora) =>
        new CobrancaProximaAlunoEvent(a.Id, a.AlunoId, a.TreinadorId, a.Valor, a.DataProximaCobranca, agora);
}
