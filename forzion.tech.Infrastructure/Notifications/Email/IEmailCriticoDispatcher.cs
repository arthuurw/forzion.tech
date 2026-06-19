using forzion.tech.Domain.Enums;

namespace forzion.tech.Infrastructure.Notifications.Email;

public interface IEmailCriticoDispatcher
{
    void Enfileirar(EmailCriticoTemplate template, string destino, string segredo);
}
