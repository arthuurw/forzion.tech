using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Domain.Entities;

public class PerfilPublico
{
    private readonly List<HorarioFuncionamento> _horariosFuncionamento = [];

    public string? NomeFantasia { get; private set; }
    public EnderecoPublico? Endereco { get; private set; }
    public IReadOnlyList<HorarioFuncionamento> HorariosFuncionamento => _horariosFuncionamento.AsReadOnly();
    public IReadOnlyDictionary<string, string>? Politicas { get; private set; }
    public bool IsPublicado { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private PerfilPublico() { }

    public static PerfilPublico CriarVazio() => new();

    public Result AtualizarDados(string? nomeFantasia, EnderecoPublico? endereco, IReadOnlyDictionary<string, string>? politicas, DateTime agora)
    {
        NomeFantasia = string.IsNullOrWhiteSpace(nomeFantasia) ? null : nomeFantasia.Trim();
        Endereco = endereco;
        Politicas = politicas;
        UpdatedAt = agora;
        return Result.Success();
    }

    public Result Publicar(DateTime agora)
    {
        if (string.IsNullOrWhiteSpace(NomeFantasia))
            return Result.Failure(PerfilPublicoErrors.NomeFantasiaObrigatorioParaPublicar);

        IsPublicado = true;
        UpdatedAt = agora;
        return Result.Success();
    }

    public void Despublicar(DateTime agora)
    {
        IsPublicado = false;
        UpdatedAt = agora;
    }

    public Result AdicionarHorario(int diaSemana, TimeOnly abreAs, TimeOnly fechaAs, DateTime agora)
    {
        var horarioResult = HorarioFuncionamento.Criar(diaSemana, abreAs, fechaAs);
        if (horarioResult.IsFailure)
            return Result.Failure(horarioResult.Error!);

        _horariosFuncionamento.Add(horarioResult.Value);
        UpdatedAt = agora;
        return Result.Success();
    }

    public Result RemoverHorario(Guid horarioId, DateTime agora)
    {
        var horario = _horariosFuncionamento.FirstOrDefault(h => h.Id == horarioId);
        if (horario is null)
            return Result.Failure(PerfilPublicoErrors.HorarioNaoEncontrado);

        _horariosFuncionamento.Remove(horario);
        UpdatedAt = agora;
        return Result.Success();
    }
}
