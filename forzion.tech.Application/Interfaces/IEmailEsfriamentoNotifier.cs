using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.Interfaces;

public interface IEmailEsfriamentoNotifier
{
    Task NotificarAsync(Guid alunoId, TipoNotificacao tipo, CancellationToken cancellationToken = default);
}
