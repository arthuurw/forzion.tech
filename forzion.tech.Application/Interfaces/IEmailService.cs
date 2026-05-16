namespace forzion.tech.Application.Interfaces;

public interface IEmailService
{
    Task EnviarAsync(string para, string assunto, string htmlBody, CancellationToken cancellationToken = default);
    bool Habilitado { get; }
}
