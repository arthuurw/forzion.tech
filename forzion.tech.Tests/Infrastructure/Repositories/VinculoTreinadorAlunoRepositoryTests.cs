using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;

namespace forzion.tech.Tests.Infrastructure.Repositories;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class VinculoTreinadorAlunoRepositoryTests(InfrastructureTestFixture fixture)
{
    private static VinculoTreinadorAlunoRepository Repo(AppDbContext ctx) => new(ctx);

    private static async Task<(Treinador treinador, Aluno aluno)> SeedParAsync(AppDbContext ctx)
    {
        var email = Email.Criar($"t{Guid.NewGuid():N}@test.com").Value;
        var contaTreinador = Conta.Criar(email, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;
        var treinador = Treinador.Criar(contaTreinador.Id, "Carlos", DateTime.UtcNow).Value;

        var emailAluno = Email.Criar($"a{Guid.NewGuid():N}@test.com").Value;
        var contaAluno = Conta.Criar(emailAluno, "hash", TipoConta.Aluno, DateTime.UtcNow).Value;
        var aluno = Aluno.Criar(contaAluno.Id, "João", DateTime.UtcNow).Value;

        await ctx.Contas.AddRangeAsync(contaTreinador, contaAluno);
        await ctx.Treinadores.AddAsync(treinador);
        await ctx.Alunos.AddAsync(aluno);
        await ctx.SaveChangesAsync();

        return (treinador, aluno);
    }

    private static async Task<Treinador> SeedTreinadorAsync(AppDbContext ctx)
    {
        var email = Email.Criar($"t{Guid.NewGuid():N}@test.com").Value;
        var conta = Conta.Criar(email, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;
        var treinador = Treinador.Criar(conta.Id, "Carlos", DateTime.UtcNow).Value;
        await ctx.Contas.AddAsync(conta);
        await ctx.Treinadores.AddAsync(treinador);
        await ctx.SaveChangesAsync();
        return treinador;
    }

    private static async Task<Aluno> SeedAlunoAsync(AppDbContext ctx)
    {
        var email = Email.Criar($"a{Guid.NewGuid():N}@test.com").Value;
        var conta = Conta.Criar(email, "hash", TipoConta.Aluno, DateTime.UtcNow).Value;
        var aluno = Aluno.Criar(conta.Id, "João", DateTime.UtcNow).Value;
        await ctx.Contas.AddAsync(conta);
        await ctx.Alunos.AddAsync(aluno);
        await ctx.SaveChangesAsync();
        return aluno;
    }

    private static async Task<Guid> SeedPacoteAsync(AppDbContext ctx, Guid treinadorId)
    {
        var pacote = Pacote.Criar(treinadorId, "Pacote Teste", 100m, DateTime.UtcNow).Value;
        await ctx.Pacotes.AddAsync(pacote);
        await ctx.SaveChangesAsync();
        return pacote.Id;
    }

    private static async Task<VinculoTreinadorAluno> AtivoAsync(AppDbContext ctx, Treinador treinador, Aluno aluno)
    {
        var pacoteId = await SeedPacoteAsync(ctx, treinador.Id);
        var vinculo = VinculoTreinadorAluno.Criar(treinador.Id, aluno.Id, DateTime.UtcNow).Value;
        vinculo.Aprovar(treinador.Id, pacoteId, DateTime.UtcNow);
        return vinculo;
    }

    private static async Task<VinculoTreinadorAluno> InativoAsync(AppDbContext ctx, Treinador treinador, Aluno aluno)
    {
        var vinculo = await AtivoAsync(ctx, treinador, aluno);
        vinculo.Inativar(DateTime.UtcNow);
        return vinculo;
    }

    private static VinculoTreinadorAluno Pendente(Treinador treinador, Aluno aluno) =>
        VinculoTreinadorAluno.Criar(treinador.Id, aluno.Id, DateTime.UtcNow).Value;

    // --- ObterAtivoPorAlunoAsync ---

    [Fact]
    public async Task ObterAtivoPorAlunoAsync_VinculoAtivo_Retorna()
    {
        await using var ctx = fixture.CreateContext();
        var (treinador, aluno) = await SeedParAsync(ctx);
        var pacoteId = await SeedPacoteAsync(ctx, treinador.Id);

        var vinculo = VinculoTreinadorAluno.Criar(treinador.Id, aluno.Id, DateTime.UtcNow).Value;
        vinculo.Aprovar(treinador.Id, pacoteId, DateTime.UtcNow);
        await ctx.VinculosTreinadorAluno.AddAsync(vinculo);
        await ctx.SaveChangesAsync();

        var resultado = await Repo(ctx).ObterAtivoPorAlunoAsync(aluno.Id);

        resultado.Should().NotBeNull();
        resultado!.AlunoId.Should().Be(aluno.Id);
        resultado.Status.Should().Be(VinculoStatus.Ativo);
    }

    [Fact]
    public async Task ObterAtivoPorAlunoAsync_VinculoPendente_RetornaNull()
    {
        await using var ctx = fixture.CreateContext();
        var (treinador, aluno) = await SeedParAsync(ctx);

        var vinculo = VinculoTreinadorAluno.Criar(treinador.Id, aluno.Id, DateTime.UtcNow).Value;
        await ctx.VinculosTreinadorAluno.AddAsync(vinculo);
        await ctx.SaveChangesAsync();

        var resultado = await Repo(ctx).ObterAtivoPorAlunoAsync(aluno.Id);

        resultado.Should().BeNull();
    }

    [Fact]
    public async Task ObterAtivoPorAlunoAsync_SemVinculo_RetornaNull()
    {
        await using var ctx = fixture.CreateContext();

        var resultado = await Repo(ctx).ObterAtivoPorAlunoAsync(Guid.NewGuid());

        resultado.Should().BeNull();
    }

    // --- ContarAtivosPorTreinadorAsync ---

    [Fact]
    public async Task ContarAtivosPorTreinadorAsync_DoisAtivos_RetornaDois()
    {
        await using var ctx = fixture.CreateContext();
        var (treinador, _) = await SeedParAsync(ctx);
        var pacoteId = await SeedPacoteAsync(ctx, treinador.Id);

        for (var i = 0; i < 2; i++)
        {
            var emailAluno = Email.Criar($"c{Guid.NewGuid():N}@test.com").Value;
            var contaAluno = Conta.Criar(emailAluno, "hash", TipoConta.Aluno, DateTime.UtcNow).Value;
            var aluno = Aluno.Criar(contaAluno.Id, $"Aluno{i}", DateTime.UtcNow).Value;
            await ctx.Contas.AddAsync(contaAluno);
            await ctx.Alunos.AddAsync(aluno);
            await ctx.SaveChangesAsync();

            var v = VinculoTreinadorAluno.Criar(treinador.Id, aluno.Id, DateTime.UtcNow).Value;
            v.Aprovar(treinador.Id, pacoteId, DateTime.UtcNow);
            await ctx.VinculosTreinadorAluno.AddAsync(v);
        }
        await ctx.SaveChangesAsync();

        var count = await Repo(ctx).ContarAtivosPorTreinadorAsync(treinador.Id);

        count.Should().Be(2);
    }

    [Fact]
    public async Task ContarAtivosPorTreinadorAsync_IsolaOutrosTreinadores()
    {
        await using var ctx = fixture.CreateContext();
        var (treinador1, aluno1) = await SeedParAsync(ctx);
        var (treinador2, aluno2) = await SeedParAsync(ctx);
        var pacote1Id = await SeedPacoteAsync(ctx, treinador1.Id);
        var pacote2Id = await SeedPacoteAsync(ctx, treinador2.Id);

        var v1 = VinculoTreinadorAluno.Criar(treinador1.Id, aluno1.Id, DateTime.UtcNow).Value;
        v1.Aprovar(treinador1.Id, pacote1Id, DateTime.UtcNow);
        var v2 = VinculoTreinadorAluno.Criar(treinador2.Id, aluno2.Id, DateTime.UtcNow).Value;
        v2.Aprovar(treinador2.Id, pacote2Id, DateTime.UtcNow);
        await ctx.VinculosTreinadorAluno.AddRangeAsync(v1, v2);
        await ctx.SaveChangesAsync();

        var count = await Repo(ctx).ContarAtivosPorTreinadorAsync(treinador1.Id);

        count.Should().Be(1);
    }

    // --- ListarComDetalhesAsync ---

    [Fact]
    public async Task ListarComDetalhesAsync_FiltroStatus_RetornaApenasStatus()
    {
        await using var ctx = fixture.CreateContext();
        var (treinador, aluno) = await SeedParAsync(ctx);

        var vinculoPendente = VinculoTreinadorAluno.Criar(treinador.Id, aluno.Id, DateTime.UtcNow).Value;
        await ctx.VinculosTreinadorAluno.AddAsync(vinculoPendente);
        await ctx.SaveChangesAsync();

        var (items, total) = await Repo(ctx).ListarComDetalhesAsync(
            treinador.Id, VinculoStatus.AguardandoAprovacao, pagina: 1, tamanhoPagina: 10);

        items.Should().NotBeEmpty();
        items.Should().AllSatisfy(v =>
            v.Vinculo.Status.Should().Be(VinculoStatus.AguardandoAprovacao));
        total.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ListarComDetalhesAsync_PaginacaoRetornaSubset()
    {
        await using var ctx = fixture.CreateContext();
        var (treinador, _) = await SeedParAsync(ctx);

        for (var i = 0; i < 5; i++)
        {
            var emailAluno = Email.Criar($"p{Guid.NewGuid():N}@test.com").Value;
            var contaAluno = Conta.Criar(emailAluno, "hash", TipoConta.Aluno, DateTime.UtcNow).Value;
            var aluno = Aluno.Criar(contaAluno.Id, $"PagAluno{i}", DateTime.UtcNow).Value;
            await ctx.Contas.AddAsync(contaAluno);
            await ctx.Alunos.AddAsync(aluno);
            await ctx.SaveChangesAsync();

            var v = VinculoTreinadorAluno.Criar(treinador.Id, aluno.Id, DateTime.UtcNow).Value;
            await ctx.VinculosTreinadorAluno.AddAsync(v);
        }
        await ctx.SaveChangesAsync();

        var (page1, total) = await Repo(ctx).ListarComDetalhesAsync(
            treinador.Id, status: null, pagina: 1, tamanhoPagina: 3);

        page1.Should().HaveCount(3);
        total.Should().BeGreaterThanOrEqualTo(5);
    }

    // --- TemVinculosAtivosAsync (gate de offboarding) ---

    [Fact]
    public async Task TemVinculosAtivosAsync_VinculoAtivo_RetornaTrue()
    {
        await using var ctx = fixture.CreateContext();
        var (treinador, aluno) = await SeedParAsync(ctx);
        var pacoteId = await SeedPacoteAsync(ctx, treinador.Id);

        var vinculo = VinculoTreinadorAluno.Criar(treinador.Id, aluno.Id, DateTime.UtcNow).Value;
        vinculo.Aprovar(treinador.Id, pacoteId, DateTime.UtcNow);
        await ctx.VinculosTreinadorAluno.AddAsync(vinculo);
        await ctx.SaveChangesAsync();

        var resultado = await Repo(ctx).TemVinculosAtivosAsync(treinador.Id);

        resultado.Should().BeTrue();
    }

    [Fact]
    public async Task TemVinculosAtivosAsync_VinculoAguardandoAprovacao_RetornaTrue()
    {
        await using var ctx = fixture.CreateContext();
        var (treinador, aluno) = await SeedParAsync(ctx);

        var vinculo = VinculoTreinadorAluno.Criar(treinador.Id, aluno.Id, DateTime.UtcNow).Value;
        await ctx.VinculosTreinadorAluno.AddAsync(vinculo);
        await ctx.SaveChangesAsync();

        var resultado = await Repo(ctx).TemVinculosAtivosAsync(treinador.Id);

        resultado.Should().BeTrue();
    }

    [Fact]
    public async Task TemVinculosAtivosAsync_SemVinculos_RetornaFalse()
    {
        await using var ctx = fixture.CreateContext();
        var (treinador, _) = await SeedParAsync(ctx);

        var resultado = await Repo(ctx).TemVinculosAtivosAsync(treinador.Id);

        resultado.Should().BeFalse();
    }

    // --- ObterAtivoAsync ---

    [Fact]
    public async Task ObterAtivoAsync_ParCorreta_Retorna()
    {
        await using var ctx = fixture.CreateContext();
        var (treinador, aluno) = await SeedParAsync(ctx);
        var pacoteId = await SeedPacoteAsync(ctx, treinador.Id);

        var vinculo = VinculoTreinadorAluno.Criar(treinador.Id, aluno.Id, DateTime.UtcNow).Value;
        vinculo.Aprovar(treinador.Id, pacoteId, DateTime.UtcNow);
        await ctx.VinculosTreinadorAluno.AddAsync(vinculo);
        await ctx.SaveChangesAsync();

        var resultado = await Repo(ctx).ObterAtivoAsync(treinador.Id, aluno.Id);

        resultado.Should().NotBeNull();
        resultado!.TreinadorId.Should().Be(treinador.Id);
        resultado.AlunoId.Should().Be(aluno.Id);
    }

    [Fact]
    public async Task ObterAtivoAsync_ParErrada_RetornaNull()
    {
        await using var ctx = fixture.CreateContext();
        var (treinador, aluno) = await SeedParAsync(ctx);
        var pacoteId = await SeedPacoteAsync(ctx, treinador.Id);

        var vinculo = VinculoTreinadorAluno.Criar(treinador.Id, aluno.Id, DateTime.UtcNow).Value;
        vinculo.Aprovar(treinador.Id, pacoteId, DateTime.UtcNow);
        await ctx.VinculosTreinadorAluno.AddAsync(vinculo);
        await ctx.SaveChangesAsync();

        var resultado = await Repo(ctx).ObterAtivoAsync(Guid.NewGuid(), aluno.Id);

        resultado.Should().BeNull();
    }

    // --- ListarAtivosEPendentesPorAlunoAsync (ANON-01) ---

    [Fact]
    public async Task ListarAtivosEPendentes_RetornaApenasAtivoEAguardando_IgnoraInativo()
    {
        await using var ctx = fixture.CreateContext();
        var aluno = await SeedAlunoAsync(ctx);

        var pendente = Pendente(await SeedTreinadorAsync(ctx), aluno);
        var ativo = await AtivoAsync(ctx, await SeedTreinadorAsync(ctx), aluno);
        var inativo = await InativoAsync(ctx, await SeedTreinadorAsync(ctx), aluno);
        await ctx.VinculosTreinadorAluno.AddRangeAsync(pendente, ativo, inativo);
        await ctx.SaveChangesAsync();

        var result = await Repo(ctx).ListarAtivosEPendentesPorAlunoAsync(aluno.Id);

        result.Should().HaveCount(2);
        result.Should().Contain(v => v.Status == VinculoStatus.AguardandoAprovacao);
        result.Should().Contain(v => v.Status == VinculoStatus.Ativo);
        result.Should().NotContain(v => v.Status == VinculoStatus.Inativo);
    }

    [Fact]
    public async Task ListarAtivosEPendentes_OutroAluno_NaoRetornaVinculosDeOutroAluno()
    {
        await using var ctx = fixture.CreateContext();
        var alvo = await SeedAlunoAsync(ctx);
        var outro = await SeedAlunoAsync(ctx);

        await ctx.VinculosTreinadorAluno.AddRangeAsync(
            Pendente(await SeedTreinadorAsync(ctx), alvo),
            Pendente(await SeedTreinadorAsync(ctx), outro));
        await ctx.SaveChangesAsync();

        var result = await Repo(ctx).ListarAtivosEPendentesPorAlunoAsync(alvo.Id);

        result.Should().HaveCount(1);
        result[0].AlunoId.Should().Be(alvo.Id);
    }

    // Sem AsNoTracking: handler de anonimização chama Inativar nas entidades retornadas.
    [Fact]
    public async Task ListarAtivosEPendentes_EntidadesTrackeadas_CallerPodeInativar()
    {
        await using var ctx = fixture.CreateContext();
        var aluno = await SeedAlunoAsync(ctx);
        var vinculo = Pendente(await SeedTreinadorAsync(ctx), aluno);
        await ctx.VinculosTreinadorAluno.AddAsync(vinculo);
        await ctx.SaveChangesAsync();

        var result = await Repo(ctx).ListarAtivosEPendentesPorAlunoAsync(aluno.Id);
        result[0].Inativar(DateTime.UtcNow);
        await ctx.SaveChangesAsync();

        var persisted = await ctx.VinculosTreinadorAluno.FindAsync(vinculo.Id);
        persisted!.Status.Should().Be(VinculoStatus.Inativo);
    }

    [Fact]
    public async Task ListarAtivosEPendentes_SemVinculos_RetornaListaVazia()
    {
        await using var ctx = fixture.CreateContext();

        var result = await Repo(ctx).ListarAtivosEPendentesPorAlunoAsync(Guid.NewGuid());

        result.Should().BeEmpty();
    }

    // --- ListarTodosPorAlunoAsync (EXP-02) ---

    [Fact]
    public async Task ListarTodosPorAluno_RetornaTodosOsStatuses()
    {
        await using var ctx = fixture.CreateContext();
        var aluno = await SeedAlunoAsync(ctx);

        var pendente = Pendente(await SeedTreinadorAsync(ctx), aluno);
        var ativo = await AtivoAsync(ctx, await SeedTreinadorAsync(ctx), aluno);
        var inativo = await InativoAsync(ctx, await SeedTreinadorAsync(ctx), aluno);
        await ctx.VinculosTreinadorAluno.AddRangeAsync(pendente, ativo, inativo);
        await ctx.SaveChangesAsync();

        var result = await Repo(ctx).ListarTodosPorAlunoAsync(aluno.Id);

        result.Should().HaveCount(3);
        result.Should().Contain(v => v.Status == VinculoStatus.AguardandoAprovacao);
        result.Should().Contain(v => v.Status == VinculoStatus.Ativo);
        result.Should().Contain(v => v.Status == VinculoStatus.Inativo);
    }

    [Fact]
    public async Task ListarTodosPorAluno_OutroAluno_NaoRetornaVinculosDeOutroAluno()
    {
        await using var ctx = fixture.CreateContext();
        var alvo = await SeedAlunoAsync(ctx);
        var outro = await SeedAlunoAsync(ctx);

        await ctx.VinculosTreinadorAluno.AddRangeAsync(
            Pendente(await SeedTreinadorAsync(ctx), alvo),
            Pendente(await SeedTreinadorAsync(ctx), outro));
        await ctx.SaveChangesAsync();

        var result = await Repo(ctx).ListarTodosPorAlunoAsync(alvo.Id);

        result.Should().HaveCount(1);
        result[0].AlunoId.Should().Be(alvo.Id);
    }

    [Fact]
    public async Task ListarTodosPorAluno_SemVinculos_RetornaListaVazia()
    {
        await using var ctx = fixture.CreateContext();

        var result = await Repo(ctx).ListarTodosPorAlunoAsync(Guid.NewGuid());

        result.Should().BeEmpty();
    }

    // --- ListarTodosPorTreinadorAsync (EXP-02) ---

    [Fact]
    public async Task ListarTodosPorTreinador_RetornaTodosOsStatuses()
    {
        await using var ctx = fixture.CreateContext();
        var treinador = await SeedTreinadorAsync(ctx);

        var pendente = Pendente(treinador, await SeedAlunoAsync(ctx));
        var ativo = await AtivoAsync(ctx, treinador, await SeedAlunoAsync(ctx));
        var inativo = await InativoAsync(ctx, treinador, await SeedAlunoAsync(ctx));
        await ctx.VinculosTreinadorAluno.AddRangeAsync(pendente, ativo, inativo);
        await ctx.SaveChangesAsync();

        var result = await Repo(ctx).ListarTodosPorTreinadorAsync(treinador.Id);

        result.Should().HaveCount(3);
        result.Should().Contain(v => v.Status == VinculoStatus.AguardandoAprovacao);
        result.Should().Contain(v => v.Status == VinculoStatus.Ativo);
        result.Should().Contain(v => v.Status == VinculoStatus.Inativo);
    }

    [Fact]
    public async Task ListarTodosPorTreinador_OutroTreinador_NaoRetornaVinculosDeOutroTreinador()
    {
        await using var ctx = fixture.CreateContext();
        var alvo = await SeedTreinadorAsync(ctx);
        var outro = await SeedTreinadorAsync(ctx);

        await ctx.VinculosTreinadorAluno.AddRangeAsync(
            Pendente(alvo, await SeedAlunoAsync(ctx)),
            Pendente(outro, await SeedAlunoAsync(ctx)));
        await ctx.SaveChangesAsync();

        var result = await Repo(ctx).ListarTodosPorTreinadorAsync(alvo.Id);

        result.Should().HaveCount(1);
        result[0].TreinadorId.Should().Be(alvo.Id);
    }

    [Fact]
    public async Task ListarTodosPorTreinador_SemVinculos_RetornaListaVazia()
    {
        await using var ctx = fixture.CreateContext();

        var result = await Repo(ctx).ListarTodosPorTreinadorAsync(Guid.NewGuid());

        result.Should().BeEmpty();
    }
}
