using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.ValueObjects;

public sealed record PoliticaAgenda
{
    public int AntecedenciaMinimaHoras { get; }
    public int HorizonteDias { get; }

    private PoliticaAgenda(int antecedenciaMinimaHoras, int horizonteDias)
    {
        AntecedenciaMinimaHoras = antecedenciaMinimaHoras;
        HorizonteDias = horizonteDias;
    }

    public static Result<PoliticaAgenda> Criar(int antecedenciaMinimaHoras, int horizonteDias)
    {
        if (antecedenciaMinimaHoras is < 0 or > 8760)
            return Result.Failure<PoliticaAgenda>(PoliticaAgendaErrors.AntecedenciaMinimaInvalida);
        if (horizonteDias is <= 0 or > 365)
            return Result.Failure<PoliticaAgenda>(PoliticaAgendaErrors.HorizonteInvalido);

        return Result.Success(new PoliticaAgenda(antecedenciaMinimaHoras, horizonteDias));
    }

    public static PoliticaAgenda Padrao() => new(2, 60);
}
