namespace forzion.tech.Application.Interfaces;

public interface IEmailService
{
    // replyTo vem DEPOIS do cancellationToken de propósito: os callers existentes passam o ct
    // como 4º argumento posicional — inserir replyTo antes quebraria todos eles.
    Task EnviarAsync(string para, string assunto, string htmlBody, CancellationToken cancellationToken = default, string? replyTo = null);
    bool Habilitado { get; }
}
