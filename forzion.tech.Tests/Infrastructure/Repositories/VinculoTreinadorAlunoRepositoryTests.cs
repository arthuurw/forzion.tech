using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;

namespace forzion.tech.Tests.Infrastructure.Repositories;

[Collection(InfrastructureTestCollection.Name)]
public class VinculoTreinadorAlunoRepositoryTests(InfrastructureTestFixture fixture)
{
    private static VinculoTreinadorAlunoRepository Repo(AppDbContext ctx) => new(ctx);

    private static async Task<(Treinador treinador, Aluno aluno)> SeedParAsync(AppDbContext ctx)
    {
        var email = Email.Criar($"t{Guid.NewGuid():N}@test.com");
        var contaTreinador = Conta.Criar(email, "hash", TipoConta.Treinador);
        var treinador = Treinador.Criar(contaTreinador.Id, "Carlos");

        var emailAluno = Email.Criar($"a{Guid.NewGuid():N}@test.com");
        var contaAluno = Conta.Criar(emailAluno, "hash", TipoConta.Aluno);
        var aluno = Aluno.Criar(contaAluno.Id, "João");

        await ctx.Contas.AddRangeAsync(contaTreinador, contaAluno);
        await ctx.Treinadores.AddAsync(treinador);
        await ctx.Alunos.AddAsync(aluno);
        await ctx.SaveChangesAsync();

        return (treinador, aluno);
    }

    private static async Task<Guid> SeedPacoteAsync(AppDbContext ctx, Guid treinadorId)
    {
        var pacote = PacoteAluno.Criar(treinadorId, "Pacote Teste", 100m);
        await ctx.PacotesAluno.AddAsync(pacote);
        await ctx.SaveChangesAsync();
        return pacote.Id;
    }

    // --- ObterAtivoPorAlunoAsync ---

    [Fact]
    public async Task ObterAtivoPorAlunoAsync_VinculoAtivo_Retorna()
    {
        await using var ctx = fixture.CreateContext();
        var (treinador, aluno) = await SeedParAsync(ctx);
        var pacoteId = await SeedPacoteAsync(ctx, treinador.Id);

        var vinculo = VinculoTreinadorAluno.Criar(treinador.Id, aluno.Id);
        vinculo.Aprovar(treinador.Id, pacoteId);
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

        var vinculo = VinculoTreinadorAluno.Criar(treinador.Id, aluno.Id);
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
            var emailAluno = Email.Criar($"c{Guid.NewGuid():N}@test.com");
            var contaAluno = Conta.Criar(emailAluno, "hash", TipoConta.Aluno);
            var aluno = Aluno.Criar(contaAluno.Id, $"Aluno{i}");
            await ctx.Contas.AddAsync(contaAluno);
            await ctx.Alunos.AddAsync(aluno);
            await ctx.SaveChangesAsync();

            var v = VinculoTreinadorAluno.Criar(treinador.Id, aluno.Id);
            v.Aprovar(treinador.Id, pacoteId);
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

        var v1 = VinculoTreinadorAluno.Criar(treinador1.Id, aluno1.Id);
        v1.Aprovar(treinador1.Id, pacote1Id);
        var v2 = VinculoTreinadorAluno.Criar(treinador2.Id, aluno2.Id);
        v2.Aprovar(treinador2.Id, pacote2Id);
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

        var vinculoPendente = VinculoTreinadorAluno.Criar(treinador.Id, aluno.Id);
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
            var emailAluno = Email.Criar($"p{Guid.NewGuid():N}@test.com");
            var contaAluno = Conta.Criar(emailAluno, "hash", TipoConta.Aluno);
            var aluno = Aluno.Criar(contaAluno.Id, $"PagAluno{i}");
            await ctx.Contas.AddAsync(contaAluno);
            await ctx.Alunos.AddAsync(aluno);
            await ctx.SaveChangesAsync();

            var v = VinculoTreinadorAluno.Criar(treinador.Id, aluno.Id);
            await ctx.VinculosTreinadorAluno.AddAsync(v);
        }
        await ctx.SaveChangesAsync();

        var (page1, total) = await Repo(ctx).ListarComDetalhesAsync(
            treinador.Id, status: null, pagina: 1, tamanhoPagina: 3);

        page1.Should().HaveCount(3);
        total.Should().BeGreaterThanOrEqualTo(5);
    }

    // --- ObterAtivoAsync ---

    [Fact]
    public async Task ObterAtivoAsync_ParCorreta_Retorna()
    {
        await using var ctx = fixture.CreateContext();
        var (treinador, aluno) = await SeedParAsync(ctx);
        var pacoteId = await SeedPacoteAsync(ctx, treinador.Id);

        var vinculo = VinculoTreinadorAluno.Criar(treinador.Id, aluno.Id);
        vinculo.Aprovar(treinador.Id, pacoteId);
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

        var vinculo = VinculoTreinadorAluno.Criar(treinador.Id, aluno.Id);
        vinculo.Aprovar(treinador.Id, pacoteId);
        await ctx.VinculosTreinadorAluno.AddAsync(vinculo);
        await ctx.SaveChangesAsync();

        var resultado = await Repo(ctx).ObterAtivoAsync(Guid.NewGuid(), aluno.Id);

        resultado.Should().BeNull();
    }
}
