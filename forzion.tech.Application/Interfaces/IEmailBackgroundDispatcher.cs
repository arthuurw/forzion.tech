namespace forzion.tech.Application.Interfaces;

public interface IEmailBackgroundDispatcher
{
    void Disparar(Func<IEmailService, CancellationToken, Task> envio);
}
