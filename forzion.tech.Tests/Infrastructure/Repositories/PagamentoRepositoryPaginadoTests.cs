using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;

namespace forzion.tech.Tests.Infrastructure.Repositories;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class PagamentoRepositoryPaginadoTests(InfrastructureTestFixture fixture)
{
    private static PagamentoRepository Repo(AppDbContext ctx) => new(ctx);

    private static async Task<Guid> SeedAssinaturaAsync(AppDbContext ctx)
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
        var assinatura = AssinaturaAluno.Criar(vinculo.Id, pacote.Id, treinador.Id, aluno.Id, 99.90m, DateTime.UtcNow).Value;
        assinatura.Ativar(DateTime.UtcNow);

        await ctx.Contas.AddRangeAsync(contaT, contaA);
        await ctx.Treinadores.AddAsync(treinador);
        await ctx.Alunos.AddAsync(aluno);
        await ctx.Pacotes.AddAsync(pacote);
        await ctx.VinculosTreinadorAluno.AddAsync(vinculo);
        await ctx.AssinaturaAlunos.AddAsync(assinatura);
        await ctx.SaveChangesAsync();
        return assinatura.Id;
    }

    private static async Task SeedPagamentoAsync(AppDbContext ctx, Guid assinaturaId, decimal valor, DateTime criadoEm)
    {
        var pagamento = Pagamento.Criar(assinaturaId, valor, criadoEm).Value;
        await ctx.Pagamentos.AddAsync(pagamento);
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task ListarPorAssinaturaAlunoPaginadoAsync_RetornaSubsetComTotal()
    {
        await using var ctx = fixture.CreateContext();
        var assinaturaId = await SeedAssinaturaAsync(ctx);
        var baseTime = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 5; i++)
            await SeedPagamentoAsync(ctx, assinaturaId, 10m + i, baseTime.AddMinutes(i));

        var (items, total) = await Repo(ctx).ListarPorAssinaturaAlunoPaginadoAsync(assinaturaId, 1, 2);

        items.Should().HaveCount(2);
        total.Should().Be(5);

        var (pagina3, _) = await Repo(ctx).ListarPorAssinaturaAlunoPaginadoAsync(assinaturaId, 3, 2);
        pagina3.Should().HaveCount(1);
    }

    [Fact]
    public async Task ListarPorAssinaturaAlunoPaginadoAsync_OrdenaCreatedAtDescendente()
    {
        await using var ctx = fixture.CreateContext();
        var assinaturaId = await SeedAssinaturaAsync(ctx);
        var t1 = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc);
        var t3 = new DateTime(2026, 5, 3, 0, 0, 0, DateTimeKind.Utc);
        await SeedPagamentoAsync(ctx, assinaturaId, 10m, t1);
        await SeedPagamentoAsync(ctx, assinaturaId, 20m, t3);
        await SeedPagamentoAsync(ctx, assinaturaId, 30m, t2);

        var (items, _) = await Repo(ctx).ListarPorAssinaturaAlunoPaginadoAsync(assinaturaId, 1, 10);

        items.Select(p => p.CreatedAt).Should().ContainInOrder(t3, t2, t1);
    }

    [Fact]
    public async Task ListarPorAssinaturaAlunoPaginadoAsync_IsolaOutraAssinatura()
    {
        await using var ctx = fixture.CreateContext();
        var assinaturaA = await SeedAssinaturaAsync(ctx);
        var assinaturaB = await SeedAssinaturaAsync(ctx);
        var baseTime = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        await SeedPagamentoAsync(ctx, assinaturaA, 10m, baseTime);
        await SeedPagamentoAsync(ctx, assinaturaB, 20m, baseTime);

        var (items, total) = await Repo(ctx).ListarPorAssinaturaAlunoPaginadoAsync(assinaturaA, 1, 10);

        total.Should().Be(1);
        items.Should().ContainSingle().Which.AssinaturaAlunoId.Should().Be(assinaturaA);
    }
}
