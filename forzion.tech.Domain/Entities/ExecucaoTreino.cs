using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Domain.Entities;

public class ExecucaoTreino
{
    private readonly List<ExecucaoExercicio> _exercicios = [];

    public Guid Id { get; private set; }
    public Guid TreinoId { get; private set; }
    public Guid AlunoId { get; private set; }
    public DateTime DataExecucao { get; private set; }
    public string? Observacao { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public IReadOnlyList<ExecucaoExercicio> Exercicios => _exercicios.AsReadOnly();

    private ExecucaoTreino() { }

    public static ExecucaoTreino Criar(Guid treinoId, Guid alunoId, DateTime dataExecucao, string? observacao = null)
    {
        if (treinoId == Guid.Empty)
            throw new DomainException("O treino é inválido.");
        if (alunoId == Guid.Empty)
            throw new DomainException("O aluno é inválido.");
        if (dataExecucao == default)
            throw new DomainException("A data de execução é inválida.");
        if (observacao is not null && observacao.Length > 500)
            throw new DomainException("A observação deve ter no máximo 500 caracteres.");

        return new ExecucaoTreino
        {
            Id = Guid.NewGuid(),
            TreinoId = treinoId,
            AlunoId = alunoId,
            DataExecucao = dataExecucao,
            Observacao = observacao,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void AdicionarExercicio(
        Guid treinoExercicioId,
        int seriesExecutadas,
        int repeticoesExecutadas,
        decimal? cargaExecutada,
        string? observacao = null)
    {
        var item = ExecucaoExercicio.Criar(
            Id, treinoExercicioId, seriesExecutadas, repeticoesExecutadas, cargaExecutada, observacao);
        _exercicios.Add(item);
    }
}
