using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Tests.Infrastructure.Repositories;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class RecebimentosTreinadorQueryTests(InfrastructureTestFixture fixture)
{
    private static PagamentoRepository Repo(AppDbContext ctx) => new(ctx);

    private static async Task<(Guid TreinadorId, string NomeAluno)> SeedAssinaturaAsync(AppDbContext ctx)
    {
        var emailT = Email.Criar($"t{Guid.NewGuid():N}@test.com").Value;
        var contaT = Conta.Criar(emailT, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;
        var treinador = Treinador.Criar(contaT.Id, $"Tr{Guid.NewGuid():N}", DateTime.UtcNow).Value;
        var emailA = Email.Criar($"a{Guid.NewGuid():N}@test.com").Value;
        var contaA = Conta.Criar(emailA, "hash", TipoConta.Aluno, DateTime.UtcNow).Value;
        var nomeAluno = $"Al{Guid.NewGuid():N}";
        var aluno = Aluno.Criar(contaA.Id, nomeAluno, DateTime.UtcNow).Value;
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
        return (treinador.Id, nomeAluno);
    }

    private static async Task<Guid> SeedPagamentoAsync(AppDbContext ctx, Guid treinadorId, decimal valor, DateTime criadoEm)
    {
        var assinatura = await ctx.AssinaturaAlunos.FirstAsync(a => a.TreinadorId == treinadorId);
        var pagamento = Pagamento.Criar(assinatura.Id, valor, criadoEm).Value;
        await ctx.Pagamentos.AddAsync(pagamento);
        await ctx.SaveChangesAsync();
        return pagamento.Id;
    }

    [Fact]
    public async Task EscopaPorTreinador_NaoVazaPagamentoDeOutro()
    {
        await using var ctx = fixture.CreateContext();
        var (t1, _) = await SeedAssinaturaAsync(ctx);
        var (t2, _) = await SeedAssinaturaAsync(ctx);
        await SeedPagamentoAsync(ctx, t1, 100m, DateTime.UtcNow);
        await SeedPagamentoAsync(ctx, t2, 200m, DateTime.UtcNow);

        var resultado = await Repo(ctx).ListarPorTreinadorAsync(t1, null, null, 50);

        resultado.Should().OnlyContain(r => r.Valor == 100m);
    }

    [Fact]
    public async Task ProjetaNomeDoAluno()
    {
        await using var ctx = fixture.CreateContext();
        var (t1, nomeAluno) = await SeedAssinaturaAsync(ctx);
        await SeedPagamentoAsync(ctx, t1, 100m, DateTime.UtcNow);

        var resultado = await Repo(ctx).ListarPorTreinadorAsync(t1, null, null, 50);

        resultado.Should().ContainSingle().Which.NomeAluno.Should().Be(nomeAluno);
    }

    [Fact]
    public async Task OrdenaPorCreatedAtDescendente()
    {
        await using var ctx = fixture.CreateContext();
        var (t1, _) = await SeedAssinaturaAsync(ctx);
        var antigo = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var recente = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        await SeedPagamentoAsync(ctx, t1, 10m, antigo);
        await SeedPagamentoAsync(ctx, t1, 20m, recente);

        var resultado = await Repo(ctx).ListarPorTreinadorAsync(t1, null, null, 50);

        resultado.Select(r => r.Valor).Should().ContainInOrder(20m, 10m);
    }

    [Fact]
    public async Task KeysetCursor_AvancaParaPaginaSeguinte()
    {
        await using var ctx = fixture.CreateContext();
        var (t1, _) = await SeedAssinaturaAsync(ctx);
        var d1 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var d2 = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var d3 = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        await SeedPagamentoAsync(ctx, t1, 10m, d1);
        await SeedPagamentoAsync(ctx, t1, 20m, d2);
        await SeedPagamentoAsync(ctx, t1, 30m, d3);

        var pagina1 = await Repo(ctx).ListarPorTreinadorAsync(t1, null, null, 2);
        pagina1.Select(r => r.Valor).Should().ContainInOrder(30m, 20m);

        var ultimo = pagina1[^1];
        var pagina2 = await Repo(ctx).ListarPorTreinadorAsync(t1, ultimo.CreatedAt, ultimo.PagamentoId, 2);

        pagina2.Should().ContainSingle().Which.Valor.Should().Be(10m);
    }
}
