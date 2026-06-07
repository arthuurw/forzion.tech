using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;

namespace forzion.tech.Tests.Infrastructure.Repositories;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class ContaRepositoryPurgaLgpdTests(InfrastructureTestFixture fixture)
{
    private static readonly DateTime Threshold = new(2021, 6, 6, 0, 0, 0, DateTimeKind.Utc);

    private static ContaRepository Repo(AppDbContext ctx) => new(ctx);

    private static async Task<(Guid ContaId, Guid AlunoId, Guid TreinadorId, Guid PacoteId, Guid VinculoId)> SeedAlunoAsync(AppDbContext ctx)
    {
        var emailT = Email.Criar($"t{Guid.NewGuid():N}@test.com").Value;
        var contaT = Conta.Criar(emailT, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;
        var treinador = Treinador.Criar(contaT.Id, $"Tr{Guid.NewGuid():N}", DateTime.UtcNow).Value;

        var emailA = Email.Criar($"a{Guid.NewGuid():N}@test.com").Value;
        var contaA = Conta.Criar(emailA, "hash", TipoConta.Aluno, DateTime.UtcNow).Value;
        var aluno = Aluno.Criar(contaA.Id, $"Al{Guid.NewGuid():N}", DateTime.UtcNow).Value;

        var pacote = Pacote.Criar(treinador.Id, $"Pac{Guid.NewGuid():N}", 99.90m, DateTime.UtcNow).Value;
        var vinculo = VinculoTreinadorAluno.Criar(treinador.Id, aluno.Id, DateTime.UtcNow).Value;
        vinculo.Aprovar(treinador.Id, pacote.Id, DateTime.UtcNow);

        await ctx.Contas.AddRangeAsync(contaT, contaA);
        await ctx.Treinadores.AddAsync(treinador);
        await ctx.Alunos.AddAsync(aluno);
        await ctx.Pacotes.AddAsync(pacote);
        await ctx.VinculosTreinadorAluno.AddAsync(vinculo);
        await ctx.SaveChangesAsync();

        return (contaA.Id, aluno.Id, treinador.Id, pacote.Id, vinculo.Id);
    }

    private static async Task<AssinaturaAluno> SeedAssinaturaAsync(
        AppDbContext ctx, (Guid ContaId, Guid AlunoId, Guid TreinadorId, Guid PacoteId, Guid VinculoId) seed,
        bool cancelar, DateTime cancelarEm)
    {
        var a = AssinaturaAluno.Criar(seed.VinculoId, seed.PacoteId, seed.TreinadorId, seed.AlunoId, 99.90m, DateTime.UtcNow).Value;
        a.Ativar(DateTime.UtcNow);
        if (cancelar) a.Cancelar(cancelarEm);
        await ctx.AssinaturaAlunos.AddAsync(a);
        await ctx.SaveChangesAsync();
        return a;
    }

    [Fact]
    public async Task ListarElegivelPurgaLgpd_TodasCanceladasAntesDoThreshold_RetornaConta()
    {
        await using var ctx = fixture.CreateContext();
        var seed = await SeedAlunoAsync(ctx);
        await SeedAssinaturaAsync(ctx, seed, cancelar: true, cancelarEm: Threshold.AddDays(-1));

        var resultado = await Repo(ctx).ListarElegivelPurgaLgpdAsync(Threshold);

        resultado.Should().Contain(seed.ContaId);
    }

    [Fact]
    public async Task ListarElegivelPurgaLgpd_CanceladaDepoisDoThreshold_NaoRetorna()
    {
        await using var ctx = fixture.CreateContext();
        var seed = await SeedAlunoAsync(ctx);
        await SeedAssinaturaAsync(ctx, seed, cancelar: true, cancelarEm: Threshold.AddDays(1));

        var resultado = await Repo(ctx).ListarElegivelPurgaLgpdAsync(Threshold);

        resultado.Should().NotContain(seed.ContaId);
    }

    [Fact]
    public async Task ListarElegivelPurgaLgpd_AlgumaAssinaturaNaoCancelada_NaoRetorna()
    {
        await using var ctx = fixture.CreateContext();
        var seed = await SeedAlunoAsync(ctx);
        await SeedAssinaturaAsync(ctx, seed, cancelar: true, cancelarEm: Threshold.AddDays(-1));
        await SeedAssinaturaAsync(ctx, seed, cancelar: false, cancelarEm: default);

        var resultado = await Repo(ctx).ListarElegivelPurgaLgpdAsync(Threshold);

        resultado.Should().NotContain(seed.ContaId);
    }

    [Fact]
    public async Task ListarElegivelPurgaLgpd_SemAssinatura_NaoRetorna()
    {
        await using var ctx = fixture.CreateContext();
        var seed = await SeedAlunoAsync(ctx);

        var resultado = await Repo(ctx).ListarElegivelPurgaLgpdAsync(Threshold);

        resultado.Should().NotContain(seed.ContaId);
    }

    [Fact]
    public async Task ListarElegivelPurgaLgpd_ContaJaAnonimizada_NaoRetorna()
    {
        await using var ctx = fixture.CreateContext();
        var seed = await SeedAlunoAsync(ctx);
        await SeedAssinaturaAsync(ctx, seed, cancelar: true, cancelarEm: Threshold.AddDays(-1));

        var conta = await ctx.Contas.FindAsync(seed.ContaId);
        conta!.Anonimizar(DateTime.UtcNow);
        await ctx.SaveChangesAsync();

        var resultado = await Repo(ctx).ListarElegivelPurgaLgpdAsync(Threshold);

        resultado.Should().NotContain(seed.ContaId);
    }
}
