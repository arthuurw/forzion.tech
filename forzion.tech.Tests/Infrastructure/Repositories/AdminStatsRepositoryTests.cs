using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;

namespace forzion.tech.Tests.Infrastructure.Repositories;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class AdminStatsRepositoryTests(InfrastructureTestFixture fixture)
{
    private static AdminStatsRepository Repo(AppDbContext ctx) => new(ctx);

    private static async Task<Guid> SeedPlanoAsync(AppDbContext ctx, TierPlano tier)
    {
        var plano = PlanoPlataforma.Criar($"Plano-{Guid.NewGuid():N}", tier, 10, 99m, DateTime.UtcNow).Value;
        await ctx.PlanosPlataforma.AddAsync(plano);
        await ctx.SaveChangesAsync();
        return plano.Id;
    }

    private static async Task SeedTreinadorAsync(AppDbContext ctx, Guid? planoPlataformaId)
    {
        var email = Email.Criar($"t{Guid.NewGuid():N}@test.com").Value;
        var conta = Conta.Criar(email, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;
        var treinador = Treinador.Criar(conta.Id, "Treinador", DateTime.UtcNow, planoPlataformaId: planoPlataformaId).Value;
        await ctx.Contas.AddAsync(conta);
        await ctx.Treinadores.AddAsync(treinador);
        await ctx.SaveChangesAsync();
    }

    private static async Task SeedAlunoAsync(AppDbContext ctx, FinalidadeTreino? finalidade)
    {
        var email = Email.Criar($"a{Guid.NewGuid():N}@test.com").Value;
        var conta = Conta.Criar(email, "hash", TipoConta.Aluno, DateTime.UtcNow).Value;
        var aluno = Aluno.Criar(conta.Id, "Aluno", DateTime.UtcNow, finalidade: finalidade).Value;
        await ctx.Contas.AddAsync(conta);
        await ctx.Alunos.AddAsync(aluno);
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task ObterDistribuicaoPorPlanoAsync_ContabilizaTreinadorComPlanoEComSemPlanoNosBucketsCorretos()
    {
        await using var ctxBaseline = fixture.CreateContext();
        var baseline = await Repo(ctxBaseline).ObterDistribuicaoPorPlanoAsync();

        await using var ctxSeed = fixture.CreateContext();
        var planoId = await SeedPlanoAsync(ctxSeed, TierPlano.Elite);
        await SeedTreinadorAsync(ctxSeed, planoId);
        await SeedTreinadorAsync(ctxSeed, null);

        await using var ctxRead = fixture.CreateContext();
        var result = await Repo(ctxRead).ObterDistribuicaoPorPlanoAsync();

        var eliteBaseline = baseline.SingleOrDefault(r => r.Tier == nameof(TierPlano.Elite))?.Total ?? 0;
        var semPlanoBaseline = baseline.SingleOrDefault(r => r.Tier == "SemPlano")?.Total ?? 0;

        result.Single(r => r.Tier == nameof(TierPlano.Elite)).Total.Should().Be(eliteBaseline + 1);
        result.Single(r => r.Tier == "SemPlano").Total.Should().Be(semPlanoBaseline + 1);
    }

    [Fact]
    public async Task ObterDistribuicaoPorFinalidadeAsync_ContabilizaAlunoComFinalidadeEComFinalidadeNulaNosBucketsCorretos()
    {
        await using var ctxBaseline = fixture.CreateContext();
        var baseline = await Repo(ctxBaseline).ObterDistribuicaoPorFinalidadeAsync();

        await using var ctxSeed = fixture.CreateContext();
        await SeedAlunoAsync(ctxSeed, FinalidadeTreino.Emagrecimento);
        await SeedAlunoAsync(ctxSeed, null);

        await using var ctxRead = fixture.CreateContext();
        var result = await Repo(ctxRead).ObterDistribuicaoPorFinalidadeAsync();

        var emagrecimentoBaseline = baseline.SingleOrDefault(r => r.Finalidade == nameof(FinalidadeTreino.Emagrecimento))?.Total ?? 0;
        var naoInformadoBaseline = baseline.SingleOrDefault(r => r.Finalidade == "NaoInformado")?.Total ?? 0;

        result.Single(r => r.Finalidade == nameof(FinalidadeTreino.Emagrecimento)).Total.Should().Be(emagrecimentoBaseline + 1);
        result.Single(r => r.Finalidade == "NaoInformado").Total.Should().Be(naoInformadoBaseline + 1);
    }
}
