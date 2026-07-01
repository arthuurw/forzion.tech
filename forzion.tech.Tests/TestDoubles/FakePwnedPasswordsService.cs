using forzion.tech.Application.Interfaces;

namespace forzion.tech.Tests.TestDoubles;

public sealed class FakePwnedPasswordsService(bool comprometida = false) : IPwnedPasswordsService
{
    public Task<bool> EstaComprometidaAsync(string senha, CancellationToken cancellationToken) =>
        Task.FromResult(comprometida);
}
