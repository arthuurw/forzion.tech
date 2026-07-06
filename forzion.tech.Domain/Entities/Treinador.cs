using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Domain.Entities;

public class Treinador : IHasDomainEvents
{
    public const int CooldownModoPagamentoDias = 90;

    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    public bool Anonimizado { get; private set; }

    public Guid Id { get; private set; }
    public Guid ContaId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public Guid? PlanoPlataformaId { get; private set; }
    public Guid? PlanoCortesiaId { get; private set; }
    public DateTime? AlunosAcimaDoCapDesde { get; private set; }
    public ModoPagamentoAluno ModoPagamentoAluno { get; private set; }
    public DateTime? ModoPagamentoAlunoAlteradoEm { get; private set; }
    public TreinadorStatus Status { get; private set; }
    public string? Telefone { get; private set; }
    public DadosFiscais? DadosFiscais { get; private set; }
    public Guid? AprovadoPorId { get; private set; }
    public DateTime? AprovadoEm { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Treinador() { }

    public static Result<Treinador> Criar(
        Guid contaId,
        string nome,
        DateTime agora,
        string? telefone = null,
        Guid? planoPlataformaId = null,
        ModoPagamentoAluno modoPagamentoAluno = ModoPagamentoAluno.Plataforma,
        bool aguardandoPagamento = false)
    {
        if (contaId == Guid.Empty)
            return Result.Failure<Treinador>(TreinadorErrors.ContaIdInvalido);
        if (string.IsNullOrWhiteSpace(nome))
            return Result.Failure<Treinador>(TreinadorErrors.NomeObrigatorio);
        if (nome.Trim().Length > 100)
            return Result.Failure<Treinador>(TreinadorErrors.NomeMuitoLongo);

        var treinador = new Treinador
        {
            Id = Guid.NewGuid(),
            ContaId = contaId,
            Nome = nome.Trim(),
            Telefone = string.IsNullOrWhiteSpace(telefone) ? null : telefone.Trim(),
            PlanoPlataformaId = planoPlataformaId,
            ModoPagamentoAluno = modoPagamentoAluno,
            Status = aguardandoPagamento ? TreinadorStatus.AguardandoPagamento : TreinadorStatus.AguardandoAprovacao,
            CreatedAt = agora
        };

        return Result.Success(treinador);
    }

    public Result ConfirmarPagamentoPlano(DateTime agora)
    {
        if (Status != TreinadorStatus.AguardandoPagamento)
            return Result.Failure(TreinadorErrors.NaoAguardandoPagamento);

        Status = TreinadorStatus.AguardandoAprovacao;
        UpdatedAt = agora;
        return Result.Success();
    }

    public Result Aprovar(Guid aprovadoPorId, DateTime agora)
    {
        if (Status != TreinadorStatus.AguardandoAprovacao)
            return Result.Failure(TreinadorErrors.NaoAguardandoAprovacaoParaAprovar);

        Status = TreinadorStatus.Ativo;
        AprovadoPorId = aprovadoPorId;
        AprovadoEm = agora;
        UpdatedAt = agora;
        _domainEvents.Add(new TreinadorAprovadoEvent(Id, aprovadoPorId, agora));
        return Result.Success();
    }

    public Result Reprovar(Guid reprovadoPorId, DateTime agora)
    {
        if (Status != TreinadorStatus.AguardandoAprovacao)
            return Result.Failure(TreinadorErrors.NaoAguardandoAprovacaoParaReprovar);

        Status = TreinadorStatus.Inativo;
        UpdatedAt = agora;
        _domainEvents.Add(new TreinadorReprovadoEvent(Id, reprovadoPorId, agora));
        return Result.Success();
    }

    public Result Inativar(DateTime agora, Guid? inativadoPorId = null)
    {
        if (Status == TreinadorStatus.Inativo)
            return Result.Failure(TreinadorErrors.JaInativo);

        Status = TreinadorStatus.Inativo;
        UpdatedAt = agora;
        _domainEvents.Add(new TreinadorInativadoEvent(Id, inativadoPorId ?? Guid.Empty, agora));
        return Result.Success();
    }

    public Result ValidarDisponibilidade()
    {
        if (Status != TreinadorStatus.Ativo)
            return Result.Failure(TreinadorErrors.NaoDisponivel);

        return Result.Success();
    }

    public Result AlterarModoPagamento(ModoPagamentoAluno novoModo, DateTime agora)
    {
        if (novoModo == ModoPagamentoAluno)
            return Result.Failure(TreinadorErrors.ModoPagamentoInalterado);

        if (ModoPagamentoAlunoAlteradoEm is { } ultima)
        {
            var liberadoEm = ultima.AddDays(CooldownModoPagamentoDias);
            if (agora < liberadoEm)
                return Result.Failure(TreinadorErrors.CooldownModoPagamento(liberadoEm));
        }

        ModoPagamentoAluno = novoModo;
        ModoPagamentoAlunoAlteradoEm = agora;
        UpdatedAt = agora;
        return Result.Success();
    }

    public Result AtribuirPlano(Guid planoPlataformaId, DateTime agora)
    {
        if (planoPlataformaId == Guid.Empty)
            return Result.Failure(TreinadorErrors.PlanoIdInvalido);
        if (Status == TreinadorStatus.Inativo)
            return Result.Failure(TreinadorErrors.PlanoTreinadorInativo);

        PlanoPlataformaId = planoPlataformaId;
        UpdatedAt = agora;
        return Result.Success();
    }

    public Result DefinirCortesia(
        Guid? planoCortesiaId,
        DateTime agora,
        decimal? precoNovoPlano = null,
        decimal? precoPlanoAtivo = null)
    {
        if (planoCortesiaId.HasValue && planoCortesiaId.Value == Guid.Empty)
            return Result.Failure(TreinadorErrors.PlanoCortesiaIdInvalido);
        if (Status == TreinadorStatus.Inativo)
            return Result.Failure(TreinadorErrors.PlanoTreinadorInativo);
        if (planoCortesiaId.HasValue && precoNovoPlano.HasValue && precoPlanoAtivo.HasValue
            && precoNovoPlano.Value < precoPlanoAtivo.Value)
            return Result.Failure(TreinadorErrors.CortesiaAbaixoDoPago);

        PlanoCortesiaId = planoCortesiaId;
        UpdatedAt = agora;
        return Result.Success();
    }

    public void MarcarAcimaDoCap(DateTime agora)
    {
        if (AlunosAcimaDoCapDesde is not null) return;

        AlunosAcimaDoCapDesde = agora;
        UpdatedAt = agora;
    }

    public void LimparAcimaDoCap(DateTime agora)
    {
        if (AlunosAcimaDoCapDesde is null) return;

        AlunosAcimaDoCapDesde = null;
        UpdatedAt = agora;
    }

    public Result Anonimizar(DateTime agora)
    {
        if (Anonimizado)
            return Result.Success();

        Anonimizado = true;
        Nome = "Usuário anonimizado";
        Telefone = null;
        UpdatedAt = agora;

        return Result.Success();
    }

    public Result ValidarParaExclusao()
    {
        if (Status != TreinadorStatus.Inativo)
            return Result.Failure(TreinadorErrors.ExclusaoApenasInativos);

        return Result.Success();
    }

    public Result DefinirDadosFiscais(DadosFiscais dadosFiscais, DateTime agora)
    {
        if (dadosFiscais is null)
            return Result.Failure(TreinadorErrors.DadosFiscaisObrigatorios);
        if (Anonimizado)
            return Result.Failure(TreinadorErrors.DadosFiscaisAnonimizado);

        DadosFiscais = dadosFiscais;
        UpdatedAt = agora;
        return Result.Success();
    }

    public Result AtualizarNome(string nome, DateTime agora)
    {
        if (string.IsNullOrWhiteSpace(nome))
            return Result.Failure(TreinadorErrors.NomeVazio);
        if (nome.Trim().Length > 100)
            return Result.Failure(TreinadorErrors.NomeMuitoLongo);

        Nome = nome.Trim();
        UpdatedAt = agora;
        return Result.Success();
    }
}
