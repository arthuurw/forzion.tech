namespace forzion.tech.Application.Interfaces;

public interface IPwnedPasswordsService
{
    Task<bool> EstaComprometidaAsync(string senha, CancellationToken cancellationToken);
}
