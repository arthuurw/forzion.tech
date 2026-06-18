using FluentAssertions;
using forzion.tech.Application.UseCases.Pagamentos;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Tests.Infrastructure.Repositories;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class PagamentoComissaoQueryTests(InfrastructureTestFixture fixture)
{
    private static readonly DateTime Inicio = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FimExclusivo = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime EmMaio = new(2026, 5, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime EmJunho = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

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
        return treinador.Id;
    }

    private static async Task SeedPagamentoAsync(AppDbContext ctx, Guid treinadorId, decimal valor, DateTime? dataPagamento)
    {
        var assinatura = await ctx.AssinaturaAlunos.FirstAsync(a => a.TreinadorId == treinadorId);
        var pagamento = Pagamento.Criar(assinatura.Id, valor, DateTime.UtcNow).Value;
        if (dataPagamento is not null)
            pagamento.MarcarPago(dataPagamento.Value);
        await ctx.Pagamentos.AddAsync(pagamento);
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task SomaFee_TruncaPorPagamento()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedAssinaturaAsync(ctx);
        await SeedPagamentoAsync(ctx, treinadorId, 12.34m, EmMaio);
        await SeedPagamentoAsync(ctx, treinadorId, 56.79m, EmMaio);

        var resultado = await Repo(ctx).ListarComissaoPorTreinadorNoPeriodoAsync(Inicio, FimExclusivo, 10m, null, 100);

        var item = resultado.Should().ContainSingle(c => c.TreinadorId == treinadorId).Subject;
        item.SomaFeeCentavos.Should().Be(690m);
    }

    [Fact]
    public async Task ForaDoPeriodo_NaoConta()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedAssinaturaAsync(ctx);
        await SeedPagamentoAsync(ctx, treinadorId, 100m, EmMaio);
        await SeedPagamentoAsync(ctx, treinadorId, 100m, EmJunho);

        var resultado = await Repo(ctx).ListarComissaoPorTreinadorNoPeriodoAsync(Inicio, FimExclusivo, 10m, null, 100);

        resultado.Should().ContainSingle(c => c.TreinadorId == treinadorId)
            .Which.SomaFeeCentavos.Should().Be(1000m);
    }

    [Fact]
    public async Task NaoPago_NaoConta()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedAssinaturaAsync(ctx);
        await SeedPagamentoAsync(ctx, treinadorId, 100m, null);

        var resultado = await Repo(ctx).ListarComissaoPorTreinadorNoPeriodoAsync(Inicio, FimExclusivo, 10m, null, 100);

        resultado.Should().NotContain(c => c.TreinadorId == treinadorId);
    }

    [Fact]
    public async Task Keyset_FiltraTreinadoresAteCursor()
    {
        await using var ctx = fixture.CreateContext();
        var t1 = await SeedAssinaturaAsync(ctx);
        var t2 = await SeedAssinaturaAsync(ctx);
        await SeedPagamentoAsync(ctx, t1, 100m, EmMaio);
        await SeedPagamentoAsync(ctx, t2, 100m, EmMaio);
        var menor = t1 < t2 ? t1 : t2;
        var maior = t1 < t2 ? t2 : t1;

        var resultado = await Repo(ctx).ListarComissaoPorTreinadorNoPeriodoAsync(Inicio, FimExclusivo, 10m, menor, 100);

        resultado.Should().Contain(c => c.TreinadorId == maior);
        resultado.Should().NotContain(c => c.TreinadorId == menor);
    }

    [Fact]
    public async Task SomaFeeCentavos_IgualSomaMoneyCentavos_ValoresComFloorStressado()
    {
        const decimal taxa = 10m;
        decimal[] valores = [29.90m, 19.99m, 9.95m, 100.00m, 49.99m];

        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedAssinaturaAsync(ctx);
        foreach (var v in valores)
            await SeedPagamentoAsync(ctx, treinadorId, v, EmMaio);

        var resultado = await Repo(ctx).ListarComissaoPorTreinadorNoPeriodoAsync(Inicio, FimExclusivo, taxa, null, 100);

        var esperado = valores.Sum(v => (decimal)MoneyCentavos.CalcularTaxaCentavos((long)(v * 100m), taxa));
        resultado.Should().ContainSingle(c => c.TreinadorId == treinadorId)
            .Which.SomaFeeCentavos.Should().Be(esperado);
    }
}
