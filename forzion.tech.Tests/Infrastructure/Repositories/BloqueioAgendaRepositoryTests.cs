using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using forzion.tech.Infrastructure.Persistence.Repositories;

namespace forzion.tech.Tests.Infrastructure.Repositories;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class BloqueioAgendaRepositoryTests(InfrastructureTestFixture fixture)
{
    private static readonly DateTime Agora = new(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

    private static BloqueioAgendaRepository Repo(AppDbContext ctx) => new(ctx);

    private static async Task<Guid> SeedTreinadorAsync(AppDbContext ctx)
    {
        var email = Email.Criar($"t{Guid.NewGuid():N}@test.com").Value;
        var conta = Conta.Criar(email, "hash", TipoConta.Treinador, Agora).Value;
        var treinador = Treinador.Criar(conta.Id, "Treinador", Agora).Value;
        await ctx.Contas.AddAsync(conta);
        await ctx.Treinadores.AddAsync(treinador);
        await ctx.SaveChangesAsync();
        return treinador.Id;
    }

    private static async Task<BloqueioAgenda> SeedBloqueioAsync(AppDbContext ctx, BloqueioAgenda bloqueio)
    {
        ctx.BloqueiosAgenda.Add(bloqueio);
        await ctx.SaveChangesAsync();
        return bloqueio;
    }

    [Fact]
    public async Task ListarVigentesAsync_PontualQueIntersectaOIntervalo_Retorna()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var pontual = await SeedBloqueioAsync(ctx,
            BloqueioAgenda.CriarPontual(treinadorId, Agora.AddHours(2), Agora.AddHours(3), null, Agora).Value);

        var vigentes = await Repo(ctx).ListarVigentesAsync(treinadorId, Agora, Agora.AddHours(24));

        vigentes.Should().ContainSingle(b => b.Id == pontual.Id);
    }

    [Fact]
    public async Task ListarVigentesAsync_PontualForaDoIntervalo_NaoRetorna()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        await SeedBloqueioAsync(ctx,
            BloqueioAgenda.CriarPontual(treinadorId, Agora.AddDays(30), Agora.AddDays(31), null, Agora).Value);

        var vigentes = await Repo(ctx).ListarVigentesAsync(treinadorId, Agora, Agora.AddHours(24));

        vigentes.Should().BeEmpty();
    }

    [Fact]
    public async Task ListarVigentesAsync_TodosOsRecorrentesRetornamIndependenteDoIntervaloConsultado()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var recorrente = await SeedBloqueioAsync(ctx,
            BloqueioAgenda.CriarRecorrente(treinadorId, (int)DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(10, 0), null, Agora).Value);

        // Intervalo consultado bem distante do "agora" do seed — recorrente não tem InicioUtc/FimUtc,
        // então não pode ser filtrado por data: é sempre vigente até ser apagado.
        var vigentes = await Repo(ctx).ListarVigentesAsync(treinadorId, Agora.AddYears(1), Agora.AddYears(1).AddHours(1));

        vigentes.Should().ContainSingle(b => b.Id == recorrente.Id);
    }

    [Fact]
    public async Task ListarVigentesAsync_BloqueioDeOutroTreinador_NaoRetorna()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorA = await SeedTreinadorAsync(ctx);
        var treinadorB = await SeedTreinadorAsync(ctx);
        await SeedBloqueioAsync(ctx,
            BloqueioAgenda.CriarPontual(treinadorB, Agora.AddHours(2), Agora.AddHours(3), null, Agora).Value);

        var vigentes = await Repo(ctx).ListarVigentesAsync(treinadorA, Agora, Agora.AddHours(24));

        vigentes.Should().BeEmpty();
    }

    [Fact]
    public async Task ObterPorIdAsync_BloqueioDoProprioTreinador_Retorna()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var bloqueio = await SeedBloqueioAsync(ctx,
            BloqueioAgenda.CriarPontual(treinadorId, Agora.AddHours(2), Agora.AddHours(3), "Motivo", Agora).Value);

        var encontrado = await Repo(ctx).ObterPorIdAsync(bloqueio.Id, treinadorId);

        encontrado.Should().NotBeNull();
        encontrado!.Id.Should().Be(bloqueio.Id);
    }

    [Fact]
    public async Task ObterPorIdAsync_BloqueioDeOutroTreinador_NaoRetorna()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorA = await SeedTreinadorAsync(ctx);
        var treinadorB = await SeedTreinadorAsync(ctx);
        var bloqueioDeB = await SeedBloqueioAsync(ctx,
            BloqueioAgenda.CriarPontual(treinadorB, Agora.AddHours(2), Agora.AddHours(3), null, Agora).Value);

        var encontrado = await Repo(ctx).ObterPorIdAsync(bloqueioDeB.Id, treinadorA);

        encontrado.Should().BeNull();
    }

    [Fact]
    public async Task ObterPorIdAsync_EntidadeRetornadaEhTracked_RemoverEComitarPersisteADelecao()
    {
        // ObterPorIdAsync alimenta o caminho de mutação (remoção) do treinador — se viesse
        // AsNoTracking, Remover(entidade) ainda funcionaria via attach, mas o precedente da
        // fatia 2 (LeadRepository) exige tracked explicitamente neste tipo de leitura.
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var bloqueio = await SeedBloqueioAsync(ctx,
            BloqueioAgenda.CriarPontual(treinadorId, Agora.AddHours(2), Agora.AddHours(3), null, Agora).Value);

        var repo = Repo(ctx);
        var carregado = await repo.ObterPorIdAsync(bloqueio.Id, treinadorId);
        repo.Remover(carregado!);
        await ctx.SaveChangesAsync();

        await using var verificacao = fixture.CreateContext();
        (await Repo(verificacao).ObterPorIdAsync(bloqueio.Id, treinadorId)).Should().BeNull();
    }

    [Fact]
    public async Task ListarPorTreinadorAsync_RetornaSoOsDoTreinadorInformado()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorA = await SeedTreinadorAsync(ctx);
        var treinadorB = await SeedTreinadorAsync(ctx);
        var deA = await SeedBloqueioAsync(ctx,
            BloqueioAgenda.CriarPontual(treinadorA, Agora.AddHours(2), Agora.AddHours(3), null, Agora).Value);
        await SeedBloqueioAsync(ctx,
            BloqueioAgenda.CriarPontual(treinadorB, Agora.AddHours(2), Agora.AddHours(3), null, Agora).Value);

        var doTreinadorA = await Repo(ctx).ListarPorTreinadorAsync(treinadorA);

        doTreinadorA.Should().ContainSingle(b => b.Id == deA.Id);
    }

    [Fact]
    public async Task AdicionarAsync_PersisteNoCommit()
    {
        await using var ctx = fixture.CreateContext();
        var treinadorId = await SeedTreinadorAsync(ctx);
        var bloqueio = BloqueioAgenda.CriarPontual(treinadorId, Agora.AddHours(2), Agora.AddHours(3), null, Agora).Value;

        await Repo(ctx).AdicionarAsync(bloqueio);
        await ctx.SaveChangesAsync();

        await using var verificacao = fixture.CreateContext();
        (await Repo(verificacao).ObterPorIdAsync(bloqueio.Id, treinadorId)).Should().NotBeNull();
    }
}
