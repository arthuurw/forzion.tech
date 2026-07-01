using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;

namespace forzion.tech.Tests.Infrastructure.Repositories;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class PacoteRepositoryTests(InfrastructureTestFixture fixture)
{
    private static PacoteRepository Repo(AppDbContext ctx) => new(ctx);

    private static async Task<Guid> SeedTreinadorAsync(AppDbContext ctx)
    {
        var email = Email.Criar($"t{Guid.NewGuid():N}@test.com").Value;
        var conta = Conta.Criar(email, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;
        var treinador = Treinador.Criar(conta.Id, "Treinador", DateTime.UtcNow).Value;
        await ctx.Contas.AddAsync(conta);
        await ctx.Treinadores.AddAsync(treinador);
        await ctx.SaveChangesAsync();
        return treinador.Id;
    }

    private static async Task<Guid> SeedAlunoAsync(AppDbContext ctx)
    {
        var email = Email.Criar($"a{Guid.NewGuid():N}@test.com").Value;
        var conta = Conta.Criar(email, "hash", TipoConta.Aluno, DateTime.UtcNow).Value;
        var aluno = Aluno.Criar(conta.Id, "Aluno", DateTime.UtcNow).Value;
        await ctx.Contas.AddAsync(conta);
        await ctx.Alunos.AddAsync(aluno);
        await ctx.SaveChangesAsync();
        return aluno.Id;
    }

    private static async Task<Pacote> SeedPacoteAsync(AppDbContext ctx, Guid treinadorId, string nome, decimal preco = 100m)
    {
        var pacote = Pacote.Criar(treinadorId, nome, preco, DateTime.UtcNow).Value;
        await ctx.Pacotes.AddAsync(pacote);
        await ctx.SaveChangesAsync();
        return pacote;
    }

    // --- ListarPorTreinadorAsync ---

    [Fact]
    public async Task ListarPorTreinadorAsync_RetornaApenasDoTreinadorOrdenadosPorNome()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorAId = await SeedTreinadorAsync(ctx);
        var treinadorBId = await SeedTreinadorAsync(ctx);
        var nomeZ = $"Z-{Guid.NewGuid():N}";
        var nomeA = $"A-{Guid.NewGuid():N}";

        var pacoteZ = await SeedPacoteAsync(ctx, treinadorAId, nomeZ);
        var pacoteA = await SeedPacoteAsync(ctx, treinadorAId, nomeA);
        await SeedPacoteAsync(ctx, treinadorBId, $"B-{Guid.NewGuid():N}");

        var result = await Repo(ctx).ListarPorTreinadorAsync(treinadorAId);

        result.Should().HaveCount(2);
        result.Select(p => p.Id).Should().Equal(pacoteA.Id, pacoteZ.Id);
    }

    [Fact]
    public async Task ListarPorTreinadorAsync_TreinadorSemPacotes_RetornaVazio()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);

        var result = await Repo(ctx).ListarPorTreinadorAsync(treinadorId);

        result.Should().BeEmpty();
    }

    // --- ListarAtivosPorTreinadorAsync ---

    [Fact]
    public async Task ListarAtivosPorTreinadorAsync_ExcluiInativos()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var ativo = await SeedPacoteAsync(ctx, treinadorId, $"Ativo-{Guid.NewGuid():N}");
        var inativo = await SeedPacoteAsync(ctx, treinadorId, $"Inativo-{Guid.NewGuid():N}");
        inativo.Inativar(DateTime.UtcNow);
        await ctx.SaveChangesAsync();

        var result = await Repo(ctx).ListarAtivosPorTreinadorAsync(treinadorId);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(ativo.Id);
    }

    // --- ExisteVinculoComPacoteAsync ---

    [Fact]
    public async Task ExisteVinculoComPacoteAsync_ComVinculoReferenciando_RetornaTrue()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var alunoId = await SeedAlunoAsync(ctx);
        var pacote = await SeedPacoteAsync(ctx, treinadorId, $"Pacote-{Guid.NewGuid():N}");

        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, alunoId, DateTime.UtcNow, pacote.Id).Value;
        await ctx.VinculosTreinadorAluno.AddAsync(vinculo);
        await ctx.SaveChangesAsync();

        var result = await Repo(ctx).ExisteVinculoComPacoteAsync(pacote.Id);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExisteVinculoComPacoteAsync_SemVinculo_RetornaFalse()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var pacote = await SeedPacoteAsync(ctx, treinadorId, $"Pacote-{Guid.NewGuid():N}");

        var result = await Repo(ctx).ExisteVinculoComPacoteAsync(pacote.Id);

        result.Should().BeFalse();
    }

    // --- ObterPorIdAsync ---

    [Fact]
    public async Task ObterPorIdAsync_Existente_Retorna()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var pacote = await SeedPacoteAsync(ctx, treinadorId, $"Pacote-{Guid.NewGuid():N}");

        var result = await Repo(ctx).ObterPorIdAsync(pacote.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(pacote.Id);
    }

    [Fact]
    public async Task ObterPorIdAsync_Inexistente_RetornaNull()
    {
        await using var ctx = fixture.CreateContext();

        var result = await Repo(ctx).ObterPorIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    // --- AdicionarAsync ---

    [Fact]
    public async Task AdicionarAsync_AposSaveChanges_RecuperavelEmNovoContexto()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var pacote = Pacote.Criar(treinadorId, $"Pacote-{Guid.NewGuid():N}", 150m, DateTime.UtcNow).Value;

        await Repo(ctx).AdicionarAsync(pacote);
        await ctx.SaveChangesAsync();

        await using var verifyCtx = fixture.CreateContext();
        var persisted = await verifyCtx.Pacotes.FindAsync(pacote.Id);
        persisted.Should().NotBeNull();
        persisted!.Nome.Should().Be(pacote.Nome);
    }

    // --- Remover ---

    [Fact]
    public async Task Remover_AposSaveChanges_NaoRecuperavelEmNovoContexto()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var pacote = await SeedPacoteAsync(ctx, treinadorId, $"Pacote-{Guid.NewGuid():N}");

        Repo(ctx).Remover(pacote);
        await ctx.SaveChangesAsync();

        await using var verifyCtx = fixture.CreateContext();
        var persisted = await verifyCtx.Pacotes.FindAsync(pacote.Id);
        persisted.Should().BeNull();
    }
}
