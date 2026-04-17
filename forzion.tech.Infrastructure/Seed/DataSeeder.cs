using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Seed;

public class DataSeeder(
    AppDbContext context,
    IPasswordHasher passwordHasher,
    IConfiguration configuration,
    ILogger<DataSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var jaExiste = await context.SystemUsers
            .AnyAsync(u => u.Role == SystemRole.SuperAdmin, cancellationToken)
            .ConfigureAwait(false);

        if (jaExiste)
            return;

        var email = configuration["Seed:AdminEmail"] ?? "admin@forzion.tech";
        var senha = configuration["Seed:AdminPassword"]
            ?? throw new InvalidOperationException("Seed:AdminPassword não configurado.");

        var conta = Conta.Criar(Email.Criar(email), passwordHasher.Hash(senha), TipoConta.SystemAdmin);
        var systemUser = SystemUser.Criar(conta.Id, "Super Admin");

        context.Contas.Add(conta);
        context.SystemUsers.Add(systemUser);
        await context.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("SuperAdmin criado: {Email}", email);
    }
}
