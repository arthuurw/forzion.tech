using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class HealthSnapshot
{
    public Guid Id { get; private set; }
    public DateTime CapturadoEm { get; private set; }
    public string Ambiente { get; private set; } = string.Empty;
    public StatusSaude StatusGeral { get; private set; }
    public string PayloadJson { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    private HealthSnapshot() { }

    public static Result<HealthSnapshot> Criar(string ambiente, StatusSaude status, string payloadJson, DateTime agora)
    {
        if (string.IsNullOrWhiteSpace(ambiente))
            return Result.Failure<HealthSnapshot>(HealthErrors.AmbienteObrigatorio);
        if (string.IsNullOrWhiteSpace(payloadJson))
            return Result.Failure<HealthSnapshot>(HealthErrors.PayloadObrigatorio);

        return Result.Success(new HealthSnapshot
        {
            Id = Guid.NewGuid(),
            CapturadoEm = agora,
            Ambiente = ambiente.Trim(),
            StatusGeral = status,
            PayloadJson = payloadJson,
            CreatedAt = agora
        });
    }
}
