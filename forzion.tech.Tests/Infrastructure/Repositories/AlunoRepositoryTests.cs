using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;

namespace forzion.tech.Tests.Infrastructure.Repositories;

[Collection(InfrastructureTestCollection.Name)]
public class AlunoRepositoryTests(InfrastructureTestFixture fixture)
{
    private static AlunoRepository Repo(AppDbContext ctx) => new(ctx);

    private static async Task<Aluno> SeedAlunoAsync(AppDbContext ctx, string nome, AlunoStatus? status = null)
    {
        var email = Email.Criar($"a{Guid.NewGuid():N}@test.com");
        var conta = Conta.Criar(email, "hash", TipoConta.Aluno, DateTime.UtcNow);
        var aluno = Aluno.Criar(conta.Id, nome, DateTime.UtcNow);
        if (status == AlunoStatus.Ativo) aluno.Ativar();
        else if (status == AlunoStatus.Inativo) { aluno.Ativar(); aluno.Inativar(); }
        await ctx.Contas.AddAsync(conta);
        await ctx.Alunos.AddAsync(aluno);
        await ctx.SaveChangesAsync();
        return aluno;
    }

    private static async Task<Guid> SeedTreinadorAsync(AppDbContext ctx)
    {
        var email = Email.Criar($"t{Guid.NewGuid():N}@test.com");
        var conta = Conta.Criar(email, "hash", TipoConta.Treinador, DateTime.UtcNow);
        var treinador = Treinador.Criar(conta.Id, "Treinador", DateTime.UtcNow);
        await ctx.Contas.AddAsync(conta);
        await ctx.Treinadores.AddAsync(treinador);
        await ctx.SaveChangesAsync();
        return treinador.Id;
    }

    private static async Task<Guid> SeedPacoteAsync(AppDbContext ctx, Guid treinadorId)
    {
        var pacote = Pacote.Criar(treinadorId, "Pacote Teste", 100m, DateTime.UtcNow);
        await ctx.Pacotes.AddAsync(pacote);
        await ctx.SaveChangesAsync();
        return pacote.Id;
    }

    // --- ListarTodosAsync ---

    [Fact]
    public async Task ListarTodosAsync_SemFiltro_RetornaTodos()
    {
        await using var ctx = fixture.CreateContext();
        await SeedAlunoAsync(ctx, $"Aluno-{Guid.NewGuid():N}");

        var (items, total) = await Repo(ctx).ListarTodosAsync(1, 100);

        items.Should().NotBeEmpty();
        total.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ListarTodosAsync_FiltroNome_RetornaApenasMatch()
    {
        await using var ctx = fixture.CreateContext();
        var suffix = Guid.NewGuid().ToString("N");
        await SeedAlunoAsync(ctx, $"Carlos-{suffix}");
        await SeedAlunoAsync(ctx, $"Maria-{suffix}");

        var (items, _) = await Repo(ctx).ListarTodosAsync(1, 100, nome: $"Carlos-{suffix}");

        items.Should().AllSatisfy(a => a.Nome.Should().Contain("Carlos"));
    }

    [Fact]
    public async Task ListarTodosAsync_FiltroStatus_RetornaApenasStatus()
    {
        await using var ctx = fixture.CreateContext();
        await SeedAlunoAsync(ctx, $"Ativo-{Guid.NewGuid():N}", AlunoStatus.Ativo);
        await SeedAlunoAsync(ctx, $"Pendente-{Guid.NewGuid():N}", AlunoStatus.AguardandoAprovacao);

        var (items, _) = await Repo(ctx).ListarTodosAsync(1, 100, status: AlunoStatus.Ativo);

        items.Should().AllSatisfy(a => a.Status.Should().Be(AlunoStatus.Ativo));
    }

    [Fact]
    public async Task ListarTodosAsync_Paginacao_RetornaSubset()
    {
        await using var ctx = fixture.CreateContext();
        for (var i = 0; i < 5; i++)
            await SeedAlunoAsync(ctx, $"PagAluno{i}-{Guid.NewGuid():N}");

        var (page, total) = await Repo(ctx).ListarTodosAsync(1, 3);

        page.Should().HaveCount(3);
        total.Should().BeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public async Task ListarTodosAsync_OrdenadoPorNome()
    {
        await using var ctx = fixture.CreateContext();
        var suffix = Guid.NewGuid().ToString("N");
        await SeedAlunoAsync(ctx, $"Zé-{suffix}");
        await SeedAlunoAsync(ctx, $"Ana-{suffix}");

        var (items, _) = await Repo(ctx).ListarTodosAsync(1, 100, nome: suffix);

        items.Should().BeInAscendingOrder(a => a.Nome);
    }

    // --- ListarPorTreinadorAsync ---

    [Fact]
    public async Task ListarPorTreinadorAsync_AlunosAtivos_Retorna()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorAsync(ctx);
        var pacoteId = await SeedPacoteAsync(ctx, tid);
        var aluno = await SeedAlunoAsync(ctx, $"AlunoAtivo-{Guid.NewGuid():N}");

        var vinculo = VinculoTreinadorAluno.Criar(tid, aluno.Id, DateTime.UtcNow);
        vinculo.Aprovar(tid, pacoteId);
        await ctx.VinculosTreinadorAluno.AddAsync(vinculo);
        await ctx.SaveChangesAsync();

        var (items, total) = await Repo(ctx).ListarPorTreinadorAsync(tid, 1, 50);

        items.Should().ContainSingle(a => a.Id == aluno.Id);
        total.Should().Be(1);
    }

    [Fact]
    public async Task ListarPorTreinadorAsync_VinculoPendente_NaoRetorna()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorAsync(ctx);
        var aluno = await SeedAlunoAsync(ctx, $"AlunoPendente-{Guid.NewGuid():N}");

        var vinculo = VinculoTreinadorAluno.Criar(tid, aluno.Id, DateTime.UtcNow);
        await ctx.VinculosTreinadorAluno.AddAsync(vinculo);
        await ctx.SaveChangesAsync();

        var (items, total) = await Repo(ctx).ListarPorTreinadorAsync(tid, 1, 50);

        items.Should().NotContain(a => a.Id == aluno.Id);
    }

    [Fact]
    public async Task ListarPorTreinadorAsync_IsolaOutrosTreinadores()
    {
        await using var ctx = fixture.CreateContext();
        var tid1 = await SeedTreinadorAsync(ctx);
        var tid2 = await SeedTreinadorAsync(ctx);
        var pacoteId = await SeedPacoteAsync(ctx, tid1);
        var aluno = await SeedAlunoAsync(ctx, $"AlunoIsolado-{Guid.NewGuid():N}");

        var vinculo = VinculoTreinadorAluno.Criar(tid1, aluno.Id, DateTime.UtcNow);
        vinculo.Aprovar(tid1, pacoteId);
        await ctx.VinculosTreinadorAluno.AddAsync(vinculo);
        await ctx.SaveChangesAsync();

        var (items, _) = await Repo(ctx).ListarPorTreinadorAsync(tid2, 1, 50);

        items.Should().NotContain(a => a.Id == aluno.Id);
    }

    [Fact]
    public async Task ListarPorTreinadorAsync_Paginacao_RetornaSubset()
    {
        await using var ctx = fixture.CreateContext();
        var tid = await SeedTreinadorAsync(ctx);
        var pacoteId = await SeedPacoteAsync(ctx, tid);

        for (var i = 0; i < 5; i++)
        {
            var aluno = await SeedAlunoAsync(ctx, $"PagAluno{i}-{Guid.NewGuid():N}");
            var v = VinculoTreinadorAluno.Criar(tid, aluno.Id, DateTime.UtcNow);
            v.Aprovar(tid, pacoteId);
            await ctx.VinculosTreinadorAluno.AddAsync(v);
        }
        await ctx.SaveChangesAsync();

        var (page, total) = await Repo(ctx).ListarPorTreinadorAsync(tid, 1, 3);

        page.Should().HaveCount(3);
        total.Should().BeGreaterThanOrEqualTo(5);
    }
}
