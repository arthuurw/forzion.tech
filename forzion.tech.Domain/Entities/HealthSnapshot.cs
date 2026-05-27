using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;

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

    public static HealthSnapshot Criar(string ambiente, StatusSaude status, string payloadJson, DateTime agora)
    {
        if (string.IsNullOrWhiteSpace(ambiente))
            throw new DomainException("O ambiente é obrigatório.");
        if (string.IsNullOrWhiteSpace(payloadJson))
            throw new DomainException("O payload é obrigatório.");

        return new HealthSnapshot
        {
            Id = Guid.NewGuid(),
            CapturadoEm = agora,
            Ambiente = ambiente.Trim(),
            StatusGeral = status,
            PayloadJson = payloadJson,
            CreatedAt = agora
        };
    }
}
