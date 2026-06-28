using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Domain.Entities;

public class Aluno : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    public bool Anonimizado { get; private set; }

    public Guid Id { get; private set; }
    public Guid ContaId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public Email? Email { get; private set; }
    public string? Telefone { get; private set; }
    public AlunoStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public int? DiasDisponiveis { get; private set; }
    public TempoDisponivel? TempoDisponivelMinutos { get; private set; }
    public FinalidadeTreino? Finalidade { get; private set; }
    public string? FocoTreino { get; private set; }
    public NivelCondicionamento? NivelCondicionamento { get; private set; }
    public string? LimitacoesFisicas { get; private set; }
    public string? Doencas { get; private set; }
    public string? ObservacoesAdicionais { get; private set; }

    private Aluno() { }

    public static Result<Aluno> Criar(
        Guid contaId,
        string nome,
        DateTime agora,
        string? email = null,
        string? telefone = null,
        int? diasDisponiveis = null,
        TempoDisponivel? tempoDisponivelMinutos = null,
        FinalidadeTreino? finalidade = null,
        string? focoTreino = null,
        NivelCondicionamento? nivelCondicionamento = null,
        string? limitacoesFisicas = null,
        string? doencas = null,
        string? observacoesAdicionais = null)
    {
        if (contaId == Guid.Empty)
            return Result.Failure<Aluno>(AlunoErrors.ContaIdInvalido);
        if (string.IsNullOrWhiteSpace(nome))
            return Result.Failure<Aluno>(AlunoErrors.NomeObrigatorio);
        if (nome.Trim().Length > 100)
            return Result.Failure<Aluno>(AlunoErrors.NomeMuitoLongo);

        var aluno = new Aluno
        {
            Id = Guid.NewGuid(),
            ContaId = contaId,
            Nome = nome.Trim(),
            Status = AlunoStatus.AguardandoAprovacao,
            CreatedAt = agora,
            DiasDisponiveis = diasDisponiveis,
            TempoDisponivelMinutos = tempoDisponivelMinutos,
            Finalidade = finalidade,
            FocoTreino = focoTreino?.Trim(),
            NivelCondicionamento = nivelCondicionamento,
            LimitacoesFisicas = limitacoesFisicas?.Trim(),
            Doencas = doencas?.Trim(),
            ObservacoesAdicionais = observacoesAdicionais?.Trim(),
        };

        if (email is not null)
        {
            var emailResult = aluno.AlterarEmail(email);
            if (emailResult.IsFailure)
                return Result.Failure<Aluno>(emailResult.Error!);
        }

        if (telefone is not null)
        {
            var telefoneResult = aluno.AlterarTelefone(telefone);
            if (telefoneResult.IsFailure)
                return Result.Failure<Aluno>(telefoneResult.Error!);
        }

        aluno._domainEvents.Add(new AlunoRegistradoEvent(aluno.Id, aluno.ContaId, aluno.Nome, aluno.Email?.Value, agora));

        return Result.Success(aluno);
    }

    public Result Atualizar(string? nome, string? email, string? telefone, DateTime agora)
    {
        var nomeOriginal = Nome;
        var emailOriginal = Email?.Value;
        var telefoneOriginal = Telefone;

        if (nome is not null)
        {
            if (string.IsNullOrWhiteSpace(nome))
                return Result.Failure(AlunoErrors.NomeVazio);
            if (nome.Trim().Length > 100)
                return Result.Failure(AlunoErrors.NomeMuitoLongo);
            Nome = nome.Trim();
        }

        if (email is not null)
        {
            var emailResult = AlterarEmail(email);
            if (emailResult.IsFailure)
                return emailResult;
        }

        if (telefone is not null)
        {
            var telefoneResult = AlterarTelefone(telefone);
            if (telefoneResult.IsFailure)
                return telefoneResult;
        }

        var houveMudanca = Nome != nomeOriginal
            || Email?.Value != emailOriginal
            || Telefone != telefoneOriginal;

        if (!houveMudanca)
            return Result.Success();

        UpdatedAt = agora;
        _domainEvents.Add(new AlunoAtualizadoEvent(Id, Nome, Email?.Value, agora));
        return Result.Success();
    }

    public Result AtualizarAnamnese(
        int? diasDisponiveis,
        TempoDisponivel? tempoDisponivelMinutos,
        FinalidadeTreino? finalidade,
        string? focoTreino,
        NivelCondicionamento? nivelCondicionamento,
        string? limitacoesFisicas,
        string? doencas,
        string? observacoesAdicionais,
        DateTime agora)
    {
        DiasDisponiveis = diasDisponiveis;
        TempoDisponivelMinutos = tempoDisponivelMinutos;
        Finalidade = finalidade;
        FocoTreino = focoTreino?.Trim();
        NivelCondicionamento = nivelCondicionamento;
        LimitacoesFisicas = limitacoesFisicas?.Trim();
        Doencas = doencas?.Trim();
        ObservacoesAdicionais = observacoesAdicionais?.Trim();
        UpdatedAt = agora;

        return Result.Success();
    }

    public Result Ativar(DateTime agora)
    {
        if (Status == AlunoStatus.Ativo)
            return Result.Failure(AlunoErrors.JaAtivo);

        Status = AlunoStatus.Ativo;
        UpdatedAt = agora;
        return Result.Success();
    }

    public Result Inativar(DateTime agora)
    {
        if (Status == AlunoStatus.Inativo)
            return Result.Failure(AlunoErrors.JaInativo);

        Status = AlunoStatus.Inativo;
        UpdatedAt = agora;
        _domainEvents.Add(new AlunoInativadoEvent(Id, agora));
        return Result.Success();
    }

    public Result Anonimizar(DateTime agora)
    {
        if (Anonimizado)
            return Result.Success();

        Anonimizado = true;
        Nome = "Usuário anonimizado";
        Email = null;
        Telefone = null;
        FocoTreino = null;
        LimitacoesFisicas = null;
        Doencas = null;
        ObservacoesAdicionais = null;
        Finalidade = null;
        NivelCondicionamento = null;
        DiasDisponiveis = null;
        TempoDisponivelMinutos = null;
        UpdatedAt = agora;

        return Result.Success();
    }

    private Result AlterarEmail(string email)
    {
        if (email.Length == 0)
        {
            Email = null;
            return Result.Success();
        }
        var emailResult = ValueObjects.Email.Criar(email);
        if (emailResult.IsFailure)
            return Result.Failure(emailResult.Error!);
        Email = emailResult.Value;
        return Result.Success();
    }

    private Result AlterarTelefone(string telefone)
    {
        if (telefone.Length == 0)
        {
            Telefone = null;
            return Result.Success();
        }
        if (telefone.Length > 20)
            return Result.Failure(AlunoErrors.TelefoneMuitoLongo);
        Telefone = telefone.Trim();
        return Result.Success();
    }
}
