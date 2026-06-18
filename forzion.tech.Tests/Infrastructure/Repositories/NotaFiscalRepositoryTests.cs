using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;

namespace forzion.tech.Tests.Infrastructure.Repositories;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class NotaFiscalRepositoryTests(InfrastructureTestFixture fixture)
{
    private static NotaFiscalRepository Repo(AppDbContext ctx) => new(ctx);

    private static async Task<Guid> SeedTreinadorAsync(AppDbContext ctx)
    {
        var email = Email.Criar($"t{Guid.NewGuid():N}@test.com").Value;
        var conta = Conta.Criar(email, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;
        var treinador = Treinador.Criar(conta.Id, $"Tr{Guid.NewGuid():N}", DateTime.UtcNow).Value;
        await ctx.Contas.AddAsync(conta);
        await ctx.Treinadores.AddAsync(treinador);
        await ctx.SaveChangesAsync();
        return treinador.Id;
    }

    private static async Task<Guid> SeedPagamentoTreinadorAsync(AppDbContext ctx, Guid treinadorId)
    {
        var plano = PlanoPlataforma.Criar($"Pl{Guid.NewGuid():N}", TierPlano.Pro, 50, 99.90m, DateTime.UtcNow).Value;
        await ctx.PlanosPlataforma.AddAsync(plano);

        var assinatura = AssinaturaTreinador.Criar(treinadorId, plano.Id, 99.90m, DateTime.UtcNow).Value;
        await ctx.AssinaturasTreinador.AddAsync(assinatura);

        var pagamento = PagamentoTreinador.Criar(
            treinadorId, assinatura.Id, 99.90m, FinalidadePagamentoTreinador.Renovacao, DateTime.UtcNow).Value;
        await ctx.PagamentosTreinador.AddAsync(pagamento);

        await ctx.SaveChangesAsync();
        return pagamento.Id;
    }

    private static async Task<NotaFiscal> SeedComissaoAsync(AppDbContext ctx, Guid treinadorId, DateOnly competenciaInicio)
    {
        var nota = NotaFiscal.CriarComissao(
            treinadorId, competenciaInicio, competenciaInicio.AddMonths(1).AddDays(-1), 50m, DateTime.UtcNow).Value;
        await ctx.NotasFiscais.AddAsync(nota);
        await ctx.SaveChangesAsync();
        return nota;
    }

    // --- AdicionarAsync / ObterPorIdAsync ---

    [Fact]
    public async Task AdicionarAsync_PersisteEObterPorIdRetorna()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var nota = NotaFiscal.CriarComissao(treinadorId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 123.45m, DateTime.UtcNow).Value;

        await Repo(ctx).AdicionarAsync(nota);
        await ctx.SaveChangesAsync();

        await using var ctx2 = fixture.CreateContext();
        var resultado = await Repo(ctx2).ObterPorIdAsync(nota.Id);

        resultado.Should().NotBeNull();
        resultado!.Id.Should().Be(nota.Id);
        resultado.TreinadorId.Should().Be(treinadorId);
        resultado.Tipo.Should().Be(TipoNotaFiscal.ComissaoMarketplace);
        resultado.Valor.Should().Be(123.45m);
        resultado.Status.Should().Be(NotaFiscalStatus.Pendente);
    }

    [Fact]
    public async Task ObterPorIdAsync_NaoExiste_RetornaNull()
    {
        await using var ctx = fixture.CreateContext();

        var resultado = await Repo(ctx).ObterPorIdAsync(Guid.NewGuid());

        resultado.Should().BeNull();
    }

    // --- ObterPorPagamentoTreinadorAsync (idempotência fluxo 1) ---

    [Fact]
    public async Task ObterPorPagamentoTreinadorAsync_Existe_Retorna()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var pagamentoId = await SeedPagamentoTreinadorAsync(ctx, treinadorId);
        var nota = NotaFiscal.CriarAssinatura(treinadorId, pagamentoId, 99.90m, DateTime.UtcNow).Value;
        await ctx.NotasFiscais.AddAsync(nota);
        await ctx.SaveChangesAsync();

        var resultado = await Repo(ctx).ObterPorPagamentoTreinadorAsync(pagamentoId);

        resultado.Should().NotBeNull();
        resultado!.Id.Should().Be(nota.Id);
        resultado.PagamentoTreinadorId.Should().Be(pagamentoId);
        resultado.Tipo.Should().Be(TipoNotaFiscal.AssinaturaSaaS);
    }

    [Fact]
    public async Task ObterPorPagamentoTreinadorAsync_NaoExiste_RetornaNull()
    {
        await using var ctx = fixture.CreateContext();

        var resultado = await Repo(ctx).ObterPorPagamentoTreinadorAsync(Guid.NewGuid());

        resultado.Should().BeNull();
    }

    // --- ListarPorTreinadorAsync ---

    [Fact]
    public async Task ListarPorTreinadorAsync_RetornaApenasDoTreinador()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var outroTreinadorId = await SeedTreinadorAsync(ctx);
        var nota = await SeedComissaoAsync(ctx, treinadorId, new DateOnly(2026, 2, 1));
        await SeedComissaoAsync(ctx, outroTreinadorId, new DateOnly(2026, 2, 1));

        var resultado = await Repo(ctx).ListarPorTreinadorAsync(treinadorId, null, 1000);

        resultado.Should().ContainSingle(n => n.Id == nota.Id);
        resultado.Should().AllSatisfy(n => n.TreinadorId.Should().Be(treinadorId));
    }

    [Fact]
    public async Task ListarPorTreinadorAsync_KeysetPaginaTodasSemDuplicarNemPular()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);

        var esperadas = new HashSet<Guid>();
        for (var i = 0; i < 5; i++)
            esperadas.Add((await SeedComissaoAsync(ctx, treinadorId, new DateOnly(2026, 1 + i, 1))).Id);

        const int limite = 2;
        var coletadas = new List<Guid>();
        Guid? aposId = null;
        while (true)
        {
            var lote = await Repo(ctx).ListarPorTreinadorAsync(treinadorId, aposId, limite);
            if (lote.Count == 0) break;
            coletadas.AddRange(lote.Select(n => n.Id));
            aposId = lote[^1].Id;
            if (lote.Count < limite) break;
        }

        coletadas.Should().OnlyHaveUniqueItems();
        coletadas.Should().BeEquivalentTo(esperadas);
    }

    // --- ListarPorStatusAsync ---

    [Fact]
    public async Task ListarPorStatusAsync_FiltraPorStatus()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var pendente1 = await SeedComissaoAsync(ctx, treinadorId, new DateOnly(2025, 1, 1));
        var pendente2 = await SeedComissaoAsync(ctx, treinadorId, new DateOnly(2025, 2, 1));
        var emitida = await SeedComissaoAsync(ctx, treinadorId, new DateOnly(2025, 3, 1));
        emitida.MarcarEmitida("CHAVE-NFSE-001", "1", DateTime.UtcNow, null, DateTime.UtcNow);
        await ctx.SaveChangesAsync();

        var coletadas = await DrenarPorStatusAsync(ctx, NotaFiscalStatus.Pendente);

        coletadas.Should().Contain(pendente1.Id).And.Contain(pendente2.Id);
        coletadas.Should().NotContain(emitida.Id);
    }

    private static async Task<List<Guid>> DrenarPorStatusAsync(AppDbContext ctx, NotaFiscalStatus status)
    {
        const int limite = 50;
        var coletadas = new List<Guid>();
        Guid? aposId = null;
        while (true)
        {
            var lote = await Repo(ctx).ListarPorStatusAsync(status, aposId, limite);
            if (lote.Count == 0) break;
            lote.Should().AllSatisfy(n => n.Status.Should().Be(status));
            coletadas.AddRange(lote.Select(n => n.Id));
            aposId = lote[^1].Id;
            if (lote.Count < limite) break;
        }
        return coletadas;
    }

    // --- ExisteComissaoAsync (idempotência fluxo 2) ---

    [Fact]
    public async Task ExisteComissaoAsync_ComissaoNaCompetencia_RetornaTrue()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var competencia = new DateOnly(2026, 4, 1);
        await SeedComissaoAsync(ctx, treinadorId, competencia);

        var existe = await Repo(ctx).ExisteComissaoAsync(treinadorId, competencia);

        existe.Should().BeTrue();
    }

    [Fact]
    public async Task ExisteComissaoAsync_CompetenciaOuTreinadorDiferente_RetornaFalse()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var outroTreinadorId = await SeedTreinadorAsync(ctx);
        var competencia = new DateOnly(2026, 5, 1);
        await SeedComissaoAsync(ctx, treinadorId, competencia);

        (await Repo(ctx).ExisteComissaoAsync(treinadorId, competencia.AddMonths(1))).Should().BeFalse();
        (await Repo(ctx).ExisteComissaoAsync(outroTreinadorId, competencia)).Should().BeFalse();
    }
}
