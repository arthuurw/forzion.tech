using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;

namespace forzion.tech.Tests.Infrastructure.Repositories;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class TentativasLoginContaRepositoryTests(InfrastructureTestFixture fixture)
{
    private static TentativasLoginContaRepository Repo(AppDbContext ctx) => new(ctx);

    private static async Task<Guid> SeedContaAsync(AppDbContext ctx)
    {
        var conta = Conta.Criar(Email.Criar($"c{Guid.NewGuid():N}@test.com").Value, "hash", TipoConta.Aluno, DateTime.UtcNow).Value;
        await ctx.Contas.AddAsync(conta);
        await ctx.SaveChangesAsync();
        return conta.Id;
    }

    [Fact]
    public async Task ObterTentativasAsync_SemRegistro_RetornaZero()
    {
        await using var ctx = fixture.CreateContext();
        var contaId = await SeedContaAsync(ctx);

        var tentativas = await Repo(ctx).ObterTentativasAsync(contaId);

        tentativas.Should().Be(0);
    }

    [Fact]
    public async Task RegistrarFalhaAsync_PrimeiraChamada_CriaLinhaComUmaTentativa()
    {
        await using var ctx = fixture.CreateContext();
        var contaId = await SeedContaAsync(ctx);

        await Repo(ctx).RegistrarFalhaAsync(contaId, DateTime.UtcNow);

        var tentativas = await Repo(ctx).ObterTentativasAsync(contaId);
        tentativas.Should().Be(1);
    }

    [Fact]
    public async Task RegistrarFalhaAsync_ChamadasSequenciais_Incrementa()
    {
        await using var ctx = fixture.CreateContext();
        var contaId = await SeedContaAsync(ctx);

        await Repo(ctx).RegistrarFalhaAsync(contaId, DateTime.UtcNow);
        await Repo(ctx).RegistrarFalhaAsync(contaId, DateTime.UtcNow);
        await Repo(ctx).RegistrarFalhaAsync(contaId, DateTime.UtcNow);

        var tentativas = await Repo(ctx).ObterTentativasAsync(contaId);
        tentativas.Should().Be(3);
    }

    // specification-concurrency §5: incremento tem que ser atômico no banco — load+increment+save
    // perderia atualizações sob corrida real. 20 chamadas EM PARALELO na MESMA conta devem
    // resultar em exatamente 20, nunca menos.
    [Fact]
    public async Task RegistrarFalhaAsync_VinteChamadasConcorrentes_ContadorChegaExatamenteAVinte()
    {
        Guid contaId;
        await using (var seedCtx = fixture.CreateContext())
            contaId = await SeedContaAsync(seedCtx);

        var tarefas = Enumerable.Range(0, 20)
            .Select(async _ =>
            {
                await using var ctx = fixture.CreateContext();
                await Repo(ctx).RegistrarFalhaAsync(contaId, DateTime.UtcNow);
            });
        await Task.WhenAll(tarefas);

        await using var verificacao = fixture.CreateContext();
        var tentativas = await Repo(verificacao).ObterTentativasAsync(contaId);
        tentativas.Should().Be(20);
    }

    [Fact]
    public async Task ZerarAsync_ResetaTentativasParaZero()
    {
        await using var ctx = fixture.CreateContext();
        var contaId = await SeedContaAsync(ctx);
        await Repo(ctx).RegistrarFalhaAsync(contaId, DateTime.UtcNow);
        await Repo(ctx).RegistrarFalhaAsync(contaId, DateTime.UtcNow);

        await Repo(ctx).ZerarAsync(contaId, DateTime.UtcNow);

        var tentativas = await Repo(ctx).ObterTentativasAsync(contaId);
        tentativas.Should().Be(0);
    }

    [Fact]
    public async Task RegistrarFalhaAsync_EhIsoladoPorConta()
    {
        await using var ctx = fixture.CreateContext();
        var contaA = await SeedContaAsync(ctx);
        var contaB = await SeedContaAsync(ctx);

        await Repo(ctx).RegistrarFalhaAsync(contaA, DateTime.UtcNow);
        await Repo(ctx).RegistrarFalhaAsync(contaA, DateTime.UtcNow);

        (await Repo(ctx).ObterTentativasAsync(contaA)).Should().Be(2);
        (await Repo(ctx).ObterTentativasAsync(contaB)).Should().Be(0);
    }
}
