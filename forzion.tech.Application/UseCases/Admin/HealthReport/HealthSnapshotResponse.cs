using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Admin.HealthReport;

public record HealthSnapshotResponse(
    Guid Id,
    DateTime CapturadoEm,
    string Ambiente,
    StatusSaude StatusGeral,
    string PayloadJson,
    bool? EmailEnviado);

public static class HealthSnapshotResponseExtensions
{
    public static HealthSnapshotResponse ToResponse(HealthSnapshot snapshot) => new(
        snapshot.Id,
        snapshot.CapturadoEm,
        snapshot.Ambiente,
        snapshot.StatusGeral,
        snapshot.PayloadJson,
        snapshot.EmailEnviado);
}
