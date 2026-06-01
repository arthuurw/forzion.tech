using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
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

    public static Result<HealthReportConfig> Criar(
        bool ativo,
        TimeOnly horaEnvioUtc,
        IEnumerable<string> destinatarios,
        bool incluirLiveness,
        bool incluirKpis,
        bool incluirEntregabilidade,
        bool incluirErros,
        DateTime agora)
    {
        var csvResult = NormalizarEValidar(destinatarios, ativo);
        if (csvResult.IsFailure)
            return Result.Failure<HealthReportConfig>(csvResult.Error!);

        return Result.Success(new HealthReportConfig
        {
            Id = Guid.NewGuid(),
            Ativo = ativo,
            HoraEnvioUtc = horaEnvioUtc,
            Destinatarios = csvResult.Value,
            IncluirLiveness = incluirLiveness,
            IncluirKpis = incluirKpis,
            IncluirEntregabilidade = incluirEntregabilidade,
            IncluirErros = incluirErros,
            CreatedAt = agora
        });
    }

    public Result Atualizar(
        bool ativo,
        TimeOnly horaEnvioUtc,
        IEnumerable<string> destinatarios,
        bool incluirLiveness,
        bool incluirKpis,
        bool incluirEntregabilidade,
        bool incluirErros,
        DateTime agora)
    {
        var csvResult = NormalizarEValidar(destinatarios, ativo);
        if (csvResult.IsFailure)
            return Result.Failure(csvResult.Error!);

        Ativo = ativo;
        HoraEnvioUtc = horaEnvioUtc;
        Destinatarios = csvResult.Value;
        IncluirLiveness = incluirLiveness;
        IncluirKpis = incluirKpis;
        IncluirEntregabilidade = incluirEntregabilidade;
        IncluirErros = incluirErros;
        UpdatedAt = agora;
        return Result.Success();
    }

    public void MarcarEnviado(DateTime agora)
    {
        UltimoEnvioEm = agora;
        UpdatedAt = agora;
    }

    public IReadOnlyList<string> ObterDestinatarios() =>
        string.IsNullOrWhiteSpace(Destinatarios)
            ? Array.Empty<string>()
            : Destinatarios.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static Result<string> NormalizarEValidar(IEnumerable<string> destinatarios, bool ativo)
    {
        var normalizados = new List<string>();
        foreach (var destinatario in (destinatarios ?? Enumerable.Empty<string>())
                     .Where(d => !string.IsNullOrWhiteSpace(d)))
        {
            var emailResult = Email.Criar(destinatario);
            if (emailResult.IsFailure)
                return Result.Failure<string>(emailResult.Error!);

            if (!normalizados.Contains(emailResult.Value.Value))
                normalizados.Add(emailResult.Value.Value);
        }

        if (ativo && normalizados.Count == 0)
            return Result.Failure<string>(HealthErrors.DestinatarioObrigatorio);

        return Result.Success(string.Join(',', normalizados));
    }
}
