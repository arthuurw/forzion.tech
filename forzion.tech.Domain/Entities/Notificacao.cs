using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class Notificacao
{
    public Guid Id { get; private set; }
    public Guid DestinatarioContaId { get; private set; }
    public TipoNotificacao Tipo { get; private set; }
    public string Titulo { get; private set; } = string.Empty;
    public string Corpo { get; private set; } = string.Empty;
    public string? LinkRelativo { get; private set; }
    public DateOnly? DiaReferencia { get; private set; }
    public bool Lida { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Notificacao() { }

    public static Result<Notificacao> Criar(
        Guid destinatarioContaId,
        TipoNotificacao tipo,
        string titulo,
        string corpo,
        DateTime agora,
        string? linkRelativo = null,
        DateOnly? diaReferencia = null)
    {
        if (destinatarioContaId == Guid.Empty)
            return Result.Failure<Notificacao>(NotificacaoErrors.DestinatarioInvalido);
        if (string.IsNullOrWhiteSpace(titulo))
            return Result.Failure<Notificacao>(NotificacaoErrors.TituloObrigatorio);
        if (titulo.Trim().Length > 120)
            return Result.Failure<Notificacao>(NotificacaoErrors.TituloMuitoLongo);
        if (string.IsNullOrWhiteSpace(corpo))
            return Result.Failure<Notificacao>(NotificacaoErrors.CorpoObrigatorio);
        if (corpo.Trim().Length > 500)
            return Result.Failure<Notificacao>(NotificacaoErrors.CorpoMuitoLongo);

        return Result.Success(new Notificacao
        {
            Id = Guid.NewGuid(),
            DestinatarioContaId = destinatarioContaId,
            Tipo = tipo,
            Titulo = titulo.Trim(),
            Corpo = corpo.Trim(),
            LinkRelativo = linkRelativo,
            DiaReferencia = diaReferencia,
            Lida = false,
            CreatedAt = agora
        });
    }

    public void MarcarLida(DateTime agora)
    {
        if (Lida)
            return;

        Lida = true;
        UpdatedAt = agora;
    }
}
