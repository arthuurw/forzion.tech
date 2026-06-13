using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class MensagemSuporte : IHasDomainEvents
{
    public const int AssuntoMinLength = 3;
    public const int AssuntoMaxLength = 120;
    public const int DescricaoMinLength = 20;
    public const int DescricaoMaxLength = 2000;

    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    public Guid Id { get; private set; }
    public Guid ContaId { get; private set; }
    public CategoriaSuporte Categoria { get; private set; }
    public string Assunto { get; private set; } = string.Empty;
    public string Descricao { get; private set; } = string.Empty;
    public DateTime CriadaEm { get; private set; }

    private MensagemSuporte() { }

    public static Result<MensagemSuporte> Criar(
        Guid contaId,
        CategoriaSuporte categoria,
        string assunto,
        string descricao,
        DateTime agora)
    {
        if (contaId == Guid.Empty)
            return Result.Failure<MensagemSuporte>(MensagemSuporteErrors.ContaIdInvalido);
        if (!Enum.IsDefined(categoria))
            return Result.Failure<MensagemSuporte>(MensagemSuporteErrors.CategoriaInvalida);

        if (string.IsNullOrWhiteSpace(assunto))
            return Result.Failure<MensagemSuporte>(MensagemSuporteErrors.AssuntoObrigatorio);
        var assuntoTrim = assunto.Trim();
        if (assuntoTrim.Length is < AssuntoMinLength or > AssuntoMaxLength)
            return Result.Failure<MensagemSuporte>(MensagemSuporteErrors.AssuntoForaDoTamanho);

        if (string.IsNullOrWhiteSpace(descricao))
            return Result.Failure<MensagemSuporte>(MensagemSuporteErrors.DescricaoObrigatoria);
        var descricaoTrim = descricao.Trim();
        if (descricaoTrim.Length is < DescricaoMinLength or > DescricaoMaxLength)
            return Result.Failure<MensagemSuporte>(MensagemSuporteErrors.DescricaoForaDoTamanho);

        var mensagem = new MensagemSuporte
        {
            Id = Guid.NewGuid(),
            ContaId = contaId,
            Categoria = categoria,
            Assunto = assuntoTrim,
            Descricao = descricaoTrim,
            CriadaEm = agora,
        };

        mensagem._domainEvents.Add(new MensagemSuporteCriadaEvent(
            mensagem.Id, mensagem.ContaId, mensagem.Categoria, mensagem.Assunto, mensagem.Descricao, agora));

        return Result.Success(mensagem);
    }
}
