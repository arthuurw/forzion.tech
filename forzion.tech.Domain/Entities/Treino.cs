using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class Treino
{
    private readonly List<TreinoExercicio> _exercicios = [];

    public Guid Id { get; private set; }
    public Guid TreinadorId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public ObjetivoTreino Objetivo { get; private set; }
    public DificuldadeTreino Dificuldade { get; private set; }
    public DateOnly? DataInicio { get; private set; }
    public DateOnly? DataFim { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public IReadOnlyList<TreinoExercicio> Exercicios => _exercicios.AsReadOnly();

    private Treino() { }

    public static Result<Treino> Criar(
        string nome,
        ObjetivoTreino objetivo,
        Guid treinadorId,
        DateTime agora,
        DificuldadeTreino dificuldade = DificuldadeTreino.Iniciante,
        DateOnly? dataInicio = null,
        DateOnly? dataFim = null)
    {
        if (string.IsNullOrWhiteSpace(nome))
            return Result.Failure<Treino>(TreinoErrors.NomeObrigatorio);
        if (nome.Trim().Length > 100)
            return Result.Failure<Treino>(TreinoErrors.NomeMuitoLongo);
        if (treinadorId == Guid.Empty)
            return Result.Failure<Treino>(TreinoErrors.TreinadorInvalido);
        if (dataInicio.HasValue && dataFim.HasValue && dataFim < dataInicio)
            return Result.Failure<Treino>(TreinoErrors.DataFimAnteriorInicio);

        return Result.Success(new Treino
        {
            Id = Guid.NewGuid(),
            TreinadorId = treinadorId,
            Nome = nome.Trim(),
            Objetivo = objetivo,
            Dificuldade = dificuldade,
            DataInicio = dataInicio,
            DataFim = dataFim,
            CreatedAt = agora
        });
    }

    public Result Atualizar(
        string? nome,
        ObjetivoTreino? objetivo,
        DateTime agora,
        DificuldadeTreino? dificuldade = null,
        DateOnly? dataInicio = null,
        DateOnly? dataFim = null,
        bool limparDataInicio = false,
        bool limparDataFim = false)
    {
        string? nomeProspectivo = null;
        if (nome is not null)
        {
            if (string.IsNullOrWhiteSpace(nome))
                return Result.Failure(TreinoErrors.NomeVazio);
            if (nome.Trim().Length > 100)
                return Result.Failure(TreinoErrors.NomeMuitoLongo);
            nomeProspectivo = nome.Trim();
        }

        var inicioProspectivo = limparDataInicio ? null : (dataInicio ?? DataInicio);
        var fimProspectivo = limparDataFim ? null : (dataFim ?? DataFim);
        if (inicioProspectivo.HasValue && fimProspectivo.HasValue && fimProspectivo < inicioProspectivo)
            return Result.Failure(TreinoErrors.DataFimAnteriorInicio);

        if (nomeProspectivo is not null)
            Nome = nomeProspectivo;
        if (objetivo is not null)
            Objetivo = objetivo.Value;
        if (dificuldade is not null)
            Dificuldade = dificuldade.Value;
        DataInicio = inicioProspectivo;
        DataFim = fimProspectivo;

        UpdatedAt = agora;
        return Result.Success();
    }

    public static Result ValidarMutabilidade(bool foiExecutado)
    {
        if (foiExecutado)
            return Result.Failure(TreinoErrors.TreinoJaExecutado);
        return Result.Success();
    }

    public Result<TreinoExercicio> AdicionarExercicio(Guid exercicioId, DateTime agora)
    {
        var ordem = _exercicios.Count + 1;
        var itemRes = TreinoExercicio.Criar(Id, exercicioId, ordem);
        if (itemRes.IsFailure)
            return Result.Failure<TreinoExercicio>(itemRes.Error!);
        var item = itemRes.Value;
        _exercicios.Add(item);
        UpdatedAt = agora;
        return Result.Success(item);
    }

    public Result RemoverExercicio(Guid treinoExercicioId, DateTime agora)
    {
        var item = _exercicios.FirstOrDefault(e => e.Id == treinoExercicioId);
        if (item is null)
            return Result.Failure(TreinoErrors.ExercicioNaoEncontrado);

        _exercicios.Remove(item);
        ReordenarExercicios();
        UpdatedAt = agora;
        return Result.Success();
    }

    public Result<Treino> Duplicar(DateTime agora)
    {
        var copia = new Treino
        {
            Id = Guid.NewGuid(),
            TreinadorId = TreinadorId,
            Nome = $"{Nome} (cópia)",
            Objetivo = Objetivo,
            Dificuldade = Dificuldade,
            CreatedAt = agora
        };

        var copiarRes = CopiarExerciciosPara(copia);
        if (copiarRes.IsFailure)
            return Result.Failure<Treino>(copiarRes.Error!);

        return Result.Success(copia);
    }

    public Result<Treino> DuplicarPara(Guid novoTreinadorId, DateTime agora)
    {
        if (novoTreinadorId == Guid.Empty)
            return Result.Failure<Treino>(TreinoErrors.TreinadorDestinoInvalido);

        var copia = new Treino
        {
            Id = Guid.NewGuid(),
            TreinadorId = novoTreinadorId,
            Nome = Nome,
            Objetivo = Objetivo,
            Dificuldade = Dificuldade,
            CreatedAt = agora
        };

        var copiarRes = CopiarExerciciosPara(copia);
        if (copiarRes.IsFailure)
            return Result.Failure<Treino>(copiarRes.Error!);

        return Result.Success(copia);
    }

    private Result CopiarExerciciosPara(Treino copia)
    {
        foreach (var e in _exercicios)
        {
            var novoExRes = TreinoExercicio.Criar(copia.Id, e.ExercicioId, e.Ordem);
            if (novoExRes.IsFailure)
                return Result.Failure(novoExRes.Error!);
            var novoEx = novoExRes.Value;

            foreach (var s in e.Series)
            {
                var serieRes = novoEx.AdicionarSerie(s.Quantidade, s.RepeticoesMin, s.RepeticoesMax, s.Descricao, s.Carga, s.Descanso);
                if (serieRes.IsFailure)
                    return Result.Failure(serieRes.Error!);
            }

            copia._exercicios.Add(novoEx);
        }

        return Result.Success();
    }

    private void ReordenarExercicios()
    {
        for (var i = 0; i < _exercicios.Count; i++)
            _exercicios[i].AlterarOrdem(i + 1);
    }
}
