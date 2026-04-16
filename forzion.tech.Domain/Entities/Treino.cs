using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Domain.Entities;

public class Treino
{
    private readonly List<TreinoExercicio> _exercicios = [];

    public Guid Id { get; private set; }
    public Guid TreinadorId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public ObjetivoTreino Objetivo { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public IReadOnlyList<TreinoExercicio> Exercicios => _exercicios.AsReadOnly();

    private Treino() { }

    public static Treino Criar(string nome, ObjetivoTreino objetivo, Guid treinadorId)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome é obrigatório.");
        if (nome.Trim().Length > 100)
            throw new DomainException("O nome deve ter no máximo 100 caracteres.");
        if (treinadorId == Guid.Empty)
            throw new DomainException("O treinador é inválido.");

        return new Treino
        {
            Id = Guid.NewGuid(),
            TreinadorId = treinadorId,
            Nome = nome.Trim(),
            Objetivo = objetivo,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Atualizar(string? nome, ObjetivoTreino? objetivo)
    {
        if (nome is not null)
        {
            if (string.IsNullOrWhiteSpace(nome))
                throw new DomainException("O nome não pode ser vazio.");
            if (nome.Trim().Length > 100)
                throw new DomainException("O nome deve ter no máximo 100 caracteres.");
            Nome = nome.Trim();
        }

        if (objetivo is not null)
            Objetivo = objetivo.Value;

        UpdatedAt = DateTime.UtcNow;
    }

    public void AdicionarExercicio(Guid exercicioId, int series, int repeticoes, decimal? carga, int? descanso)
    {
        var ordem = _exercicios.Count + 1;
        var item = TreinoExercicio.Criar(Id, exercicioId, series, repeticoes, carga, descanso, ordem);
        _exercicios.Add(item);
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoverExercicio(Guid treinoExercicioId)
    {
        var item = _exercicios.FirstOrDefault(e => e.Id == treinoExercicioId)
            ?? throw new DomainException("Exercício não encontrado no treino.");

        _exercicios.Remove(item);
        ReordenarExercicios();
        UpdatedAt = DateTime.UtcNow;
    }

    public Treino Duplicar()
    {
        var copia = new Treino
        {
            Id = Guid.NewGuid(),
            TreinadorId = TreinadorId,
            Nome = $"{Nome} (cópia)",
            Objetivo = Objetivo,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var e in _exercicios)
            copia._exercicios.Add(
                TreinoExercicio.Criar(copia.Id, e.ExercicioId, e.Series, e.Repeticoes, e.Carga, e.Descanso, e.Ordem));

        return copia;
    }

    private void ReordenarExercicios()
    {
        for (var i = 0; i < _exercicios.Count; i++)
            _exercicios[i].AlterarOrdem(i + 1);
    }
}
