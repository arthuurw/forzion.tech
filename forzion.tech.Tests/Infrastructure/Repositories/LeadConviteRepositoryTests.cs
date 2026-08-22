using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;

namespace forzion.tech.Tests.Infrastructure.Repositories;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class LeadConviteRepositoryTests(InfrastructureTestFixture fixture)
{
    private static readonly DateTime Agora = new(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

    private static LeadConviteRepository Repo(AppDbContext ctx) => new(ctx);

    private static async Task<Guid> SeedTreinadorAsync(AppDbContext ctx)
    {
        var email = Email.Criar($"t{Guid.NewGuid():N}@test.com").Value;
        var conta = Conta.Criar(email, "hash", TipoConta.Treinador, Agora).Value;
        var treinador = Treinador.Criar(conta.Id, "Treinador", Agora).Value;
        await ctx.Contas.AddAsync(conta);
        await ctx.Treinadores.AddAsync(treinador);
        await ctx.SaveChangesAsync();
        return treinador.Id;
    }

    private static async Task<Guid> SeedLeadAsync(AppDbContext ctx, Guid treinadorId)
    {
        var lead = Lead.Criar(
            treinadorId, "Fulano",
            ContatoLead.Criar(TipoContatoLead.Email, $"{Guid.NewGuid():N}@lead.com").Value,
            null, ConsentimentoLead.Criar("Contato comercial", Agora, Agora).Value,
            null, LeadSource.Agent, null, null, Agora).Value;
        ctx.Set<Lead>().Add(lead);
        await ctx.SaveChangesAsync();
        return lead.Id;
    }

    private static async Task<LeadConvite> SeedConviteAsync(AppDbContext ctx, Guid leadId, Guid treinadorId, string tokenHash, DateTime expiraEm)
    {
        var convite = LeadConvite.Criar(leadId, treinadorId, tokenHash, expiraEm, Agora).Value;
        ctx.Set<LeadConvite>().Add(convite);
        await ctx.SaveChangesAsync();
        return convite;
    }

    [Fact]
    public async Task ObterPorTokenHashAsync_TokenExiste_Retorna()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var leadId = await SeedLeadAsync(ctx, treinadorId);
        var hash = $"hash-{Guid.NewGuid():N}";
        var convite = await SeedConviteAsync(ctx, leadId, treinadorId, hash, Agora.AddDays(14));

        var encontrado = await Repo(ctx).ObterPorTokenHashAsync(hash);

        encontrado.Should().NotBeNull();
        encontrado!.Id.Should().Be(convite.Id);
    }

    [Fact]
    public async Task ObterPorTokenHashAsync_TokenInexistente_RetornaNull()
    {
        await using var ctx = fixture.CreateContext();

        var encontrado = await Repo(ctx).ObterPorTokenHashAsync($"hash-{Guid.NewGuid():N}");

        encontrado.Should().BeNull();
    }

    [Fact]
    public async Task ObterAtivoPorLeadAsync_ConviteNaoConsumidoNemInvalidado_Retorna()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var leadId = await SeedLeadAsync(ctx, treinadorId);
        var convite = await SeedConviteAsync(ctx, leadId, treinadorId, $"hash-{Guid.NewGuid():N}", Agora.AddDays(14));

        var ativo = await Repo(ctx).ObterAtivoPorLeadAsync(treinadorId, leadId);

        ativo.Should().NotBeNull();
        ativo!.Id.Should().Be(convite.Id);
    }

    [Fact]
    public async Task ObterAtivoPorLeadAsync_ConviteInvalidado_NaoRetorna()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var leadId = await SeedLeadAsync(ctx, treinadorId);
        var convite = await SeedConviteAsync(ctx, leadId, treinadorId, $"hash-{Guid.NewGuid():N}", Agora.AddDays(14));
        convite.Invalidar(Agora.AddHours(1));
        await ctx.SaveChangesAsync();

        var ativo = await Repo(ctx).ObterAtivoPorLeadAsync(treinadorId, leadId);

        ativo.Should().BeNull();
    }

    [Fact]
    public async Task LimparExpiradosOuConsumidosAsync_RemoveExpiradoEConsumido_PreservaPendenteValido()
    {
        var connectionString = await fixture.CriarBancoIsoladoAsync();
        await using var ctx = fixture.CreateContext(connectionString);
        var treinadorId = await SeedTreinadorAsync(ctx);
        var leadId = await SeedLeadAsync(ctx, treinadorId);

        var expirado = await SeedConviteAsync(ctx, leadId, treinadorId, "hash-expirado", Agora.AddDays(1));
        var consumido = await SeedConviteAsync(ctx, leadId, treinadorId, "hash-consumido", Agora.AddDays(14));
        consumido.Consumir(Agora.AddHours(1));
        await ctx.SaveChangesAsync();
        var pendente = await SeedConviteAsync(ctx, leadId, treinadorId, "hash-pendente", Agora.AddDays(14));

        var agoraDaLimpeza = Agora.AddDays(2);
        var removidos = await Repo(ctx).LimparExpiradosOuConsumidosAsync(agoraDaLimpeza);

        removidos.Should().Be(2);

        // ExecuteDeleteAsync roda SQL direto, sem passar pelo change tracker — reler no MESMO
        // ctx (ainda com as entidades antigas em cache) daria falso "ainda existe".
        await using var verificacao = fixture.CreateContext(connectionString);
        (await verificacao.Set<LeadConvite>().FindAsync(expirado.Id)).Should().BeNull();
        (await verificacao.Set<LeadConvite>().FindAsync(consumido.Id)).Should().BeNull();
        (await verificacao.Set<LeadConvite>().FindAsync(pendente.Id)).Should().NotBeNull();
    }
}
