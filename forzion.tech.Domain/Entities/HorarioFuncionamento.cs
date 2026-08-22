using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class HorarioFuncionamento
{
    public Guid Id { get; private set; }
    public int DiaSemana { get; private set; }
    public TimeOnly AbreAs { get; private set; }
    public TimeOnly FechaAs { get; private set; }

    private HorarioFuncionamento() { }

    public static Result<HorarioFuncionamento> Criar(int diaSemana, TimeOnly abreAs, TimeOnly fechaAs)
    {
        if (diaSemana is < 0 or > 6)
            return Result.Failure<HorarioFuncionamento>(HorarioFuncionamentoErrors.DiaSemanaInvalido);
        if (abreAs >= fechaAs)
            return Result.Failure<HorarioFuncionamento>(HorarioFuncionamentoErrors.IntervaloInvalido);

        return Result.Success(new HorarioFuncionamento
        {
            Id = Guid.NewGuid(),
            DiaSemana = diaSemana,
            AbreAs = abreAs,
            FechaAs = fechaAs
        });
    }
}
