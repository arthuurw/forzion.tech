using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Assinaturas;

public record AssinaturaResponse(
    Guid AssinaturaId,
    Guid VinculoId,
    Guid PacoteAlunoId,
    Guid TreinadorId,
    Guid AlunoId,
    decimal Valor,
    AssinaturaStatus Status,
    DateTime DataInicio,
    DateTime DataProximaCobranca,
    DateTime? DataCancelamento,
    DateTime CreatedAt);

public static class AssinaturaResponseExtensions
{
    public static AssinaturaResponse ToResponse(Assinatura a) => new(
        a.Id, a.VinculoId, a.PacoteAlunoId, a.TreinadorId, a.AlunoId,
        a.Valor, a.Status, a.DataInicio, a.DataProximaCobranca, a.DataCancelamento, a.CreatedAt);
}
