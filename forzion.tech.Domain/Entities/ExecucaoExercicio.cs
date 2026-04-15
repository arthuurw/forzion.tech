using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Domain.Entities;

public class ExecucaoExercicio
{
    public Guid Id { get; private set; }
    public Guid ExecucaoTreinoId { get; private set; }
    public Guid TreinoExercicioId { get; private set; }
    public int SeriesExecutadas { get; private set; }
    public int RepeticoesExecutadas { get; private set; }
    public decimal? CargaExecutada { get; private set; }
    public string? Observacao { get; private set; }

    private ExecucaoExercicio() { }

    internal static ExecucaoExercicio Criar(
        Guid execucaoTreinoId,
        Guid treinoExercicioId,
        int seriesExecutadas,
        int repeticoesExecutadas,
        decimal? cargaExecutada,
        string? observacao)
    {
        if (execucaoTreinoId == Guid.Empty)
            throw new DomainException("A execução é inválida.");
        if (treinoExercicioId == Guid.Empty)
            throw new DomainException("O exercício do treino é inválido.");
        if (seriesExecutadas < 1)
            throw new DomainException("O número de séries deve ser maior que zero.");
        if (repeticoesExecutadas < 1)
            throw new DomainException("O número de repetições deve ser maior que zero.");
        if (cargaExecutada is not null && cargaExecutada < 0)
            throw new DomainException("A carga não pode ser negativa.");
        if (observacao is not null && observacao.Length > 500)
            throw new DomainException("A observação deve ter no máximo 500 caracteres.");

        return new ExecucaoExercicio
        {
            Id = Guid.NewGuid(),
            ExecucaoTreinoId = execucaoTreinoId,
            TreinoExercicioId = treinoExercicioId,
            SeriesExecutadas = seriesExecutadas,
            RepeticoesExecutadas = repeticoesExecutadas,
            CargaExecutada = cargaExecutada,
            Observacao = observacao
        };
    }
}
