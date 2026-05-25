using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.AssinaturaAlunos;

public record AssinaturaAlunoResponse(
    Guid AssinaturaAlunoId,
    Guid VinculoId,
    Guid PacoteId,
    Guid TreinadorId,
    Guid AlunoId,
    decimal Valor,
    AssinaturaAlunoStatus Status,
    DateTime DataInicio,
    DateTime DataProximaCobranca,
    DateTime? DataCancelamento,
    DateTime CreatedAt);

public static class AssinaturaAlunoResponseExtensions
{
    public static AssinaturaAlunoResponse ToResponse(AssinaturaAluno a) => new(
        a.Id, a.VinculoId, a.PacoteId, a.TreinadorId, a.AlunoId,
        a.Valor, a.Status, a.DataInicio, a.DataProximaCobranca, a.DataCancelamento, a.CreatedAt);
}
