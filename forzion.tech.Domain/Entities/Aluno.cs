using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Domain.Entities;

public class Aluno : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

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

    public static Aluno Criar(
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
            throw new DomainException("O identificador da conta é inválido.");
        if (string.IsNullOrWhiteSpace(nome))
            throw new DomainException("O nome é obrigatório.");
        if (nome.Trim().Length > 100)
            throw new DomainException("O nome deve ter no máximo 100 caracteres.");

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
            aluno.AlterarEmail(email);

        if (telefone is not null)
            aluno.AlterarTelefone(telefone);

        aluno._domainEvents.Add(new AlunoRegistradoEvent(aluno.Id, aluno.Nome, aluno.Email?.Value, agora));

        return aluno;
    }

    public void Atualizar(string? nome, string? email, string? telefone)
    {
        if (nome is not null)
        {
            if (string.IsNullOrWhiteSpace(nome))
                throw new DomainException("O nome não pode ser vazio.");
            if (nome.Trim().Length > 100)
                throw new DomainException("O nome deve ter no máximo 100 caracteres.");
            Nome = nome.Trim();
        }

        if (email is not null)
            AlterarEmail(email);

        if (telefone is not null)
            AlterarTelefone(telefone);

        UpdatedAt = DateTime.UtcNow;
        _domainEvents.Add(new AlunoAtualizadoEvent(Id, Nome, Email?.Value, DateTime.UtcNow));
    }

    public void Ativar()
    {
        if (Status == AlunoStatus.Ativo)
            throw new DomainException("O aluno já está ativo.");

        Status = AlunoStatus.Ativo;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Inativar()
    {
        if (Status == AlunoStatus.Inativo)
            throw new DomainException("O aluno já está inativo.");

        Status = AlunoStatus.Inativo;
        UpdatedAt = DateTime.UtcNow;
        _domainEvents.Add(new AlunoInativadoEvent(Id, DateTime.UtcNow));
    }

    private void AlterarEmail(string email)
    {
        if (email.Length == 0)
        {
            Email = null;
            return;
        }
        Email = ValueObjects.Email.Criar(email);
    }

    private void AlterarTelefone(string telefone)
    {
        if (telefone.Length == 0)
        {
            Telefone = null;
            return;
        }
        if (telefone.Length > 20)
            throw new DomainException("O telefone deve ter no máximo 20 caracteres.");
        Telefone = telefone.Trim();
    }
}
