using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;

namespace forzion.tech.Tests.Infrastructure.Repositories;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class ContaRecebimentoPendentesOnboardingQueryTests(InfrastructureTestFixture fixture)
{
    private static async Task<ContaRecebimento> SeedContaAsync(
        AppDbContext ctx, bool configurada, bool onboardingCompleto, DateTime criadaEm)
    {
        var conta = Conta.Criar(Email.Criar($"t{Guid.NewGuid():N}@test.com").Value, "hash", TipoConta.Treinador, criadaEm).Value;
        var treinador = Treinador.Criar(conta.Id, $"Tr{Guid.NewGuid():N}", criadaEm).Value;
        var contaRecebimento = ContaRecebimento.Criar(treinador.Id, criadaEm).Value;
        if (configurada)
            contaRecebimento.ConfigurarStripeConnect($"acct_{Guid.NewGuid():N}", criadaEm);
        if (onboardingCompleto)
            contaRecebimento.ConfirmarOnboarding(criadaEm);

        await ctx.Contas.AddAsync(conta);
        await ctx.Treinadores.AddAsync(treinador);
        await ctx.ContasRecebimento.AddAsync(contaRecebimento);
        await ctx.SaveChangesAsync();
        return contaRecebimento;
    }

    [Fact]
    public async Task RetornaSomenteConfiguradasNaoCompletas()
    {
        await using var ctx = fixture.CreateContext();
        var agora = DateTime.UtcNow;
        var pendente = await SeedContaAsync(ctx, configurada: true, onboardingCompleto: false, agora);
        await SeedContaAsync(ctx, configurada: true, onboardingCompleto: true, agora);
        await SeedContaAsync(ctx, configurada: false, onboardingCompleto: false, agora);

        var resultado = await new ContaRecebimentoRepository(ctx).ListarConfiguradasPendentesOnboardingAsync(100);

        resultado.Should().Contain(c => c.Id == pendente.Id);
        resultado.Should().OnlyContain(c => c.Configurada && !c.OnboardingCompleto);
    }

    [Fact]
    public async Task RespeitaCapDeBatch()
    {
        await using var ctx = fixture.CreateContext();
        var agora = DateTime.UtcNow;
        await SeedContaAsync(ctx, configurada: true, onboardingCompleto: false, agora);
        await SeedContaAsync(ctx, configurada: true, onboardingCompleto: false, agora.AddSeconds(1));
        await SeedContaAsync(ctx, configurada: true, onboardingCompleto: false, agora.AddSeconds(2));

        var resultado = await new ContaRecebimentoRepository(ctx).ListarConfiguradasPendentesOnboardingAsync(2);

        resultado.Should().HaveCount(2);
    }
}
