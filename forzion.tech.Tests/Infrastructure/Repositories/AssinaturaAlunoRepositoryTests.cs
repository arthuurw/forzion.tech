using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;

namespace forzion.tech.Tests.Infrastructure.Repositories;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class AssinaturaAlunoRepositoryTests(InfrastructureTestFixture fixture)
{
    private static AssinaturaAlunoRepository Repo(AppDbContext ctx) => new(ctx);

    private record SeedCtx(Guid TreinadorId, Guid AlunoId, Guid PacoteId, Guid VinculoId);

    private static async Task<SeedCtx> SeedContextAsync(AppDbContext ctx, Guid? existingAlunoId = null)
    {
        var emailT = Email.Criar($"t{Guid.NewGuid():N}@test.com").Value;
        var contaT = Conta.Criar(emailT, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;
        var treinador = Treinador.Criar(contaT.Id, $"Tr{Guid.NewGuid():N}", DateTime.UtcNow).Value;
        await ctx.Contas.AddAsync(contaT);
        await ctx.Treinadores.AddAsync(treinador);

        Guid alunoId;
        if (existingAlunoId is null)
        {
            var emailA = Email.Criar($"a{Guid.NewGuid():N}@test.com").Value;
            var contaA = Conta.Criar(emailA, "hash", TipoConta.Aluno, DateTime.UtcNow).Value;
            var aluno = Aluno.Criar(contaA.Id, $"Al{Guid.NewGuid():N}", DateTime.UtcNow).Value;
            await ctx.Contas.AddAsync(contaA);
            await ctx.Alunos.AddAsync(aluno);
            alunoId = aluno.Id;
        }
        else
        {
            alunoId = existingAlunoId.Value;
        }

        var pacote = Pacote.Criar(treinador.Id, $"Pac{Guid.NewGuid():N}", 99.90m, DateTime.UtcNow).Value;
        await ctx.Pacotes.AddAsync(pacote);

        var vinculo = VinculoTreinadorAluno.Criar(treinador.Id, alunoId, DateTime.UtcNow).Value;
        vinculo.Aprovar(treinador.Id, pacote.Id, DateTime.UtcNow);
        await ctx.VinculosTreinadorAluno.AddAsync(vinculo);

        await ctx.SaveChangesAsync();
        return new SeedCtx(treinador.Id, alunoId, pacote.Id, vinculo.Id);
    }

    private static async Task<AssinaturaAluno> SeedAssinaturaAlunoAsync(
        AppDbContext ctx,
        SeedCtx seed,
        bool ativa = true,
        bool cancelada = false)
    {
        var a = AssinaturaAluno.Criar(seed.VinculoId, seed.PacoteId, seed.TreinadorId, seed.AlunoId, 99.90m, DateTime.UtcNow).Value;
        if (ativa) a.Ativar(DateTime.UtcNow);
        if (cancelada) a.Cancelar(DateTime.UtcNow);
        await ctx.AssinaturaAlunos.AddAsync(a);
        await ctx.SaveChangesAsync();
        return a;
    }

    // --- ObterPorIdAsync ---

    [Fact]
    public async Task ObterPorIdAsync_Existe_Retorna()
    {
        await using var ctx = fixture.CreateContext();
        var seed = await SeedContextAsync(ctx);
        var assinatura = await SeedAssinaturaAlunoAsync(ctx, seed);

        var resultado = await Repo(ctx).ObterPorIdAsync(assinatura.Id);

        resultado.Should().NotBeNull();
        resultado!.Id.Should().Be(assinatura.Id);
        resultado.AlunoId.Should().Be(seed.AlunoId);
    }

    [Fact]
    public async Task ObterPorIdAsync_NaoExiste_RetornaNull()
    {
        await using var ctx = fixture.CreateContext();

        var resultado = await Repo(ctx).ObterPorIdAsync(Guid.NewGuid());

        resultado.Should().BeNull();
    }

    // --- ObterPorVinculoIdAsync ---

    [Fact]
    public async Task ObterPorVinculoIdAsync_Existe_Retorna()
    {
        await using var ctx = fixture.CreateContext();
        var seed = await SeedContextAsync(ctx);
        var assinatura = await SeedAssinaturaAlunoAsync(ctx, seed);

        var resultado = await Repo(ctx).ObterPorVinculoIdAsync(seed.VinculoId);

        resultado.Should().NotBeNull();
        resultado!.Id.Should().Be(assinatura.Id);
        resultado.VinculoId.Should().Be(seed.VinculoId);
    }

    [Fact]
    public async Task ObterPorVinculoIdAsync_NaoExiste_RetornaNull()
    {
        await using var ctx = fixture.CreateContext();

        var resultado = await Repo(ctx).ObterPorVinculoIdAsync(Guid.NewGuid());

        resultado.Should().BeNull();
    }

    // --- ListarParaRenovarAsync ---

    [Fact]
    public async Task ListarParaRenovarAsync_AtivaComCobrancaPassada_Retorna()
    {
        await using var ctx = fixture.CreateContext();
        var seed = await SeedContextAsync(ctx);
        // Criar com Status=Pendente; Ativar() → Status=Ativa; DataProximaCobranca=DateTime.UtcNow (passado)
        var assinatura = await SeedAssinaturaAlunoAsync(ctx, seed, ativa: true);

        var resultado = await Repo(ctx).ListarParaRenovarAsync(DateTime.UtcNow.AddMinutes(1), null, 1000);

        resultado.Should().Contain(a => a.Id == assinatura.Id);
    }

    [Fact]
    public async Task ListarParaRenovarAsync_AtivaComCobrancaFutura_NaoRetorna()
    {
        await using var ctx = fixture.CreateContext();
        var seed = await SeedContextAsync(ctx);
        var assinatura = await SeedAssinaturaAlunoAsync(ctx, seed, ativa: true);

        // Agendar cobrança para 1 mês no futuro
        assinatura.AgendarProximaCobranca(DateTime.UtcNow.AddMonths(1), DateTime.UtcNow);
        await ctx.SaveChangesAsync();

        var resultado = await Repo(ctx).ListarParaRenovarAsync(DateTime.UtcNow, null, 1000);

        resultado.Should().NotContain(a => a.Id == assinatura.Id);
    }

    [Fact]
    public async Task ListarParaRenovarAsync_StatusCancelada_NaoRetorna()
    {
        await using var ctx = fixture.CreateContext();
        var seed = await SeedContextAsync(ctx);
        // Ativar e então cancelar → Status=Cancelada, DataProximaCobranca ainda no passado
        var assinatura = await SeedAssinaturaAlunoAsync(ctx, seed, ativa: true, cancelada: true);

        var resultado = await Repo(ctx).ListarParaRenovarAsync(DateTime.UtcNow.AddMinutes(1), null, 1000);

        resultado.Should().NotContain(a => a.Id == assinatura.Id);
    }

    [Fact]
    public async Task ListarParaRenovarAsync_StatusPendente_NaoRetorna()
    {
        await using var ctx = fixture.CreateContext();
        var seed = await SeedContextAsync(ctx);
        // Não chamar Ativar() → Status=Pendente
        var assinatura = await SeedAssinaturaAlunoAsync(ctx, seed, ativa: false);

        var resultado = await Repo(ctx).ListarParaRenovarAsync(DateTime.UtcNow.AddMinutes(1), null, 1000);

        resultado.Should().NotContain(a => a.Id == assinatura.Id);
    }

    [Fact]
    public async Task ListarParaRenovarAsync_KeysetPaginaTodasSemDuplicarNemPular()
    {
        // PERF-01: prova que o loop keyset do cron processa todas as devidas em > 1 lote, sem
        // duplicar nem pular (DB do fixture acumula entre testes → asserções defensivas: subset+único).
        await using var ctx = fixture.CreateContext();
        var ate = DateTime.UtcNow.AddMinutes(1);

        var esperadas = new HashSet<Guid>();
        for (var i = 0; i < 5; i++)
        {
            var seed = await SeedContextAsync(ctx);
            esperadas.Add((await SeedAssinaturaAlunoAsync(ctx, seed, ativa: true)).Id);
        }
        var canceladaSeed = await SeedContextAsync(ctx);
        var cancelada = await SeedAssinaturaAlunoAsync(ctx, canceladaSeed, ativa: true, cancelada: true);

        const int limite = 2;
        var coletadas = new List<Guid>();
        Guid? aposId = null;
        while (true)
        {
            var lote = await Repo(ctx).ListarParaRenovarAsync(ate, aposId, limite);
            if (lote.Count == 0) break;
            coletadas.AddRange(lote.Select(a => a.Id));
            aposId = lote[^1].Id;
            if (lote.Count < limite) break;
        }

        coletadas.Should().OnlyHaveUniqueItems();
        esperadas.Should().BeSubsetOf(coletadas);
        coletadas.Should().NotContain(cancelada.Id);
    }

    // --- ListarPorAlunoAsync ---

    [Fact]
    public async Task ListarPorAlunoAsync_RetornaApenasDoAluno()
    {
        await using var ctx = fixture.CreateContext();

        // Seed aluno1 com 1 assinatura
        var seed1 = await SeedContextAsync(ctx);
        var a1 = await SeedAssinaturaAlunoAsync(ctx, seed1);

        // Seed aluno2 (diferente) com 1 assinatura — não deve aparecer na lista do aluno1
        var seed2 = await SeedContextAsync(ctx);
        await SeedAssinaturaAlunoAsync(ctx, seed2);

        var resultado = await Repo(ctx).ListarPorAlunoAsync(seed1.AlunoId);

        resultado.Should().HaveCount(1);
        resultado[0].Id.Should().Be(a1.Id);
        resultado.Should().AllSatisfy(a => a.AlunoId.Should().Be(seed1.AlunoId));
    }

    [Fact]
    public async Task ListarPorAlunoAsync_MultiplaAssinaturaAlunos_OrdenaPorCreatedAtDesc()
    {
        await using var ctx = fixture.CreateContext();

        // Mesmo aluno, dois treinadores diferentes → dois vínculos distintos
        var seed1 = await SeedContextAsync(ctx);
        var a1 = await SeedAssinaturaAlunoAsync(ctx, seed1);

        var seed2 = await SeedContextAsync(ctx, existingAlunoId: seed1.AlunoId);
        var a2 = await SeedAssinaturaAlunoAsync(ctx, seed2);

        var resultado = await Repo(ctx).ListarPorAlunoAsync(seed1.AlunoId);

        resultado.Should().HaveCount(2);
        resultado.Should().AllSatisfy(a => a.AlunoId.Should().Be(seed1.AlunoId));
        // Ordenação desc: a2 criado depois de a1 → a2 deve ser primeiro
        resultado[0].Id.Should().Be(a2.Id);
        resultado[1].Id.Should().Be(a1.Id);
    }
}
