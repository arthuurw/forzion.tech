using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Domain.Entities;

public class HealthReportConfig
{
    public Guid Id { get; private set; }
    public bool Ativo { get; private set; }
    public TimeOnly HoraEnvioUtc { get; private set; }
    public string Destinatarios { get; private set; } = string.Empty;
    public bool IncluirLiveness { get; private set; }
    public bool IncluirKpis { get; private set; }
    public bool IncluirEntregabilidade { get; private set; }
    public bool IncluirErros { get; private set; }
    public DateTime? UltimoEnvioEm { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private HealthReportConfig() { }

    public static HealthReportConfig Criar(
        bool ativo,
        TimeOnly horaEnvioUtc,
        IEnumerable<string> destinatarios,
        bool incluirLiveness,
        bool incluirKpis,
        bool incluirEntregabilidade,
        bool incluirErros,
        DateTime agora)
    {
        var csv = NormalizarEValidar(destinatarios, ativo);

        return new HealthReportConfig
        {
            Id = Guid.NewGuid(),
            Ativo = ativo,
            HoraEnvioUtc = horaEnvioUtc,
            Destinatarios = csv,
            IncluirLiveness = incluirLiveness,
            IncluirKpis = incluirKpis,
            IncluirEntregabilidade = incluirEntregabilidade,
            IncluirErros = incluirErros,
            CreatedAt = agora
        };
    }

    public void Atualizar(
        bool ativo,
        TimeOnly horaEnvioUtc,
        IEnumerable<string> destinatarios,
        bool incluirLiveness,
        bool incluirKpis,
        bool incluirEntregabilidade,
        bool incluirErros)
    {
        var csv = NormalizarEValidar(destinatarios, ativo);

        Ativo = ativo;
        HoraEnvioUtc = horaEnvioUtc;
        Destinatarios = csv;
        IncluirLiveness = incluirLiveness;
        IncluirKpis = incluirKpis;
        IncluirEntregabilidade = incluirEntregabilidade;
        IncluirErros = incluirErros;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarcarEnviado(DateTime agora)
    {
        UltimoEnvioEm = agora;
        UpdatedAt = DateTime.UtcNow;
    }

    public IReadOnlyList<string> ObterDestinatarios() =>
        string.IsNullOrWhiteSpace(Destinatarios)
            ? Array.Empty<string>()
            : Destinatarios.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string NormalizarEValidar(IEnumerable<string> destinatarios, bool ativo)
    {
        var normalizados = (destinatarios ?? Enumerable.Empty<string>())
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => Email.Criar(d).Value)
            .Distinct()
            .ToList();

        if (ativo && normalizados.Count == 0)
            throw new DomainException("Uma configuração ativa exige ao menos um destinatário.");

        return string.Join(',', normalizados);
    }
}
