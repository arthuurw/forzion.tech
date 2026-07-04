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
public class NotificacaoRepositoryTests(InfrastructureTestFixture fixture)
{
    private static NotificacaoRepository Repo(AppDbContext ctx) => new(ctx, new NpgsqlDatabaseErrorInspector());

    private static async Task<Guid> SeedContaAsync(AppDbContext ctx)
    {
        var email = Email.Criar($"n{Guid.NewGuid():N}@test.com").Value;
        var conta = Conta.Criar(email, "hash", TipoConta.Aluno, DateTime.UtcNow).Value;
        await ctx.Contas.AddAsync(conta);
        await ctx.SaveChangesAsync();
        return conta.Id;
    }

    private static Notificacao NovaNotificacao(
        Guid contaId, TipoNotificacao tipo, DateTime agora, DateOnly? dia = null) =>
        Notificacao.Criar(contaId, tipo, "titulo", "corpo", agora, null, dia).Value;

    [Fact]
    public async Task AdicionarAsync_Persiste()
    {
        await using var ctx = fixture.CreateContext();
        var contaId = await SeedContaAsync(ctx);
        var notif = NovaNotificacao(contaId, TipoNotificacao.NovoTreino, DateTime.UtcNow);

        await Repo(ctx).AdicionarAsync(notif);

        await using var verifica = fixture.CreateContext();
        var persisted = await verifica.Notificacoes.FindAsync(notif.Id);
        persisted.Should().NotBeNull();
        persisted!.DestinatarioContaId.Should().Be(contaId);
        persisted.Lida.Should().BeFalse();
    }

    [Fact]
    public async Task AdicionarAsync_SegundoInsertMesmoContaTipoDia_NoOpIdempotente()
    {
        await using var ctx = fixture.CreateContext();
        var contaId = await SeedContaAsync(ctx);
        var dia = new DateOnly(2026, 7, 3);

        await Repo(ctx).AdicionarAsync(NovaNotificacao(contaId, TipoNotificacao.Reforco, DateTime.UtcNow, dia));
        await Repo(ctx).AdicionarAsync(NovaNotificacao(contaId, TipoNotificacao.Reforco, DateTime.UtcNow, dia));

        await using var verifica = fixture.CreateContext();
        var total = await verifica.Notificacoes
            .CountAsync(n => n.DestinatarioContaId == contaId && n.Tipo == TipoNotificacao.Reforco);
        total.Should().Be(1);
    }

    [Fact]
    public async Task AdicionarAsync_DiaReferenciaNula_NaoDeduplica()
    {
        await using var ctx = fixture.CreateContext();
        var contaId = await SeedContaAsync(ctx);

        await Repo(ctx).AdicionarAsync(NovaNotificacao(contaId, TipoNotificacao.NovoTreino, DateTime.UtcNow));
        await Repo(ctx).AdicionarAsync(NovaNotificacao(contaId, TipoNotificacao.NovoTreino, DateTime.UtcNow));

        await using var verifica = fixture.CreateContext();
        var total = await verifica.Notificacoes
            .CountAsync(n => n.DestinatarioContaId == contaId && n.Tipo == TipoNotificacao.NovoTreino);
        total.Should().Be(2);
    }

    [Fact]
    public async Task ContarNaoLidasAsync_ContaSomenteNaoLidasDaConta()
    {
        await using var ctx = fixture.CreateContext();
        var contaId = await SeedContaAsync(ctx);
        var outraContaId = await SeedContaAsync(ctx);

        var lida = NovaNotificacao(contaId, TipoNotificacao.NovoTreino, DateTime.UtcNow);
        lida.MarcarLida(DateTime.UtcNow);
        await Repo(ctx).AdicionarAsync(lida);
        await Repo(ctx).AdicionarAsync(NovaNotificacao(contaId, TipoNotificacao.ExecucaoRegistrada, DateTime.UtcNow));
        await Repo(ctx).AdicionarAsync(NovaNotificacao(outraContaId, TipoNotificacao.NovoTreino, DateTime.UtcNow));

        var count = await Repo(ctx).ContarNaoLidasAsync(contaId);

        count.Should().Be(1);
    }

    [Fact]
    public async Task ContarNaoLidasAsync_ContaSemNotificacoes_RetornaZero()
    {
        await using var ctx = fixture.CreateContext();
        var contaId = await SeedContaAsync(ctx);

        var count = await Repo(ctx).ContarNaoLidasAsync(contaId);

        count.Should().Be(0);
    }

    [Fact]
    public async Task ListarPorContaAsync_ScopedPaginadoOrdenaMaisRecentePrimeiro()
    {
        await using var ctx = fixture.CreateContext();
        var contaId = await SeedContaAsync(ctx);
        var outraContaId = await SeedContaAsync(ctx);
        var baseTime = DateTime.UtcNow.AddMinutes(-10);

        var maisAntiga = NovaNotificacao(contaId, TipoNotificacao.NovoTreino, baseTime);
        var meio = NovaNotificacao(contaId, TipoNotificacao.Reforco, baseTime.AddMinutes(2));
        var maisNova = NovaNotificacao(contaId, TipoNotificacao.LembreteLeve, baseTime.AddMinutes(4));
        await Repo(ctx).AdicionarAsync(maisAntiga);
        await Repo(ctx).AdicionarAsync(meio);
        await Repo(ctx).AdicionarAsync(maisNova);
        await Repo(ctx).AdicionarAsync(NovaNotificacao(outraContaId, TipoNotificacao.NovoTreino, baseTime.AddMinutes(3)));

        var page = await Repo(ctx).ListarPorContaAsync(contaId, 0, 2);

        page.Select(n => n.Id).Should().Equal(maisNova.Id, meio.Id);
    }

    [Fact]
    public async Task ListarPorContaAsync_RespeitaSkip()
    {
        await using var ctx = fixture.CreateContext();
        var contaId = await SeedContaAsync(ctx);
        var baseTime = DateTime.UtcNow.AddMinutes(-10);

        var maisAntiga = NovaNotificacao(contaId, TipoNotificacao.NovoTreino, baseTime);
        var meio = NovaNotificacao(contaId, TipoNotificacao.Reforco, baseTime.AddMinutes(2));
        var maisNova = NovaNotificacao(contaId, TipoNotificacao.LembreteLeve, baseTime.AddMinutes(4));
        await Repo(ctx).AdicionarAsync(maisAntiga);
        await Repo(ctx).AdicionarAsync(meio);
        await Repo(ctx).AdicionarAsync(maisNova);

        var page = await Repo(ctx).ListarPorContaAsync(contaId, 1, 5);

        page.Select(n => n.Id).Should().Equal(meio.Id, maisAntiga.Id);
    }

    [Fact]
    public async Task MarcarLidaAsync_DonoDaNotificacao_MarcaLidaEDerrubaContador()
    {
        await using var ctx = fixture.CreateContext();
        var contaId = await SeedContaAsync(ctx);
        var notif = NovaNotificacao(contaId, TipoNotificacao.NovoTreino, DateTime.UtcNow);
        await Repo(ctx).AdicionarAsync(notif);
        var agora = DateTime.UtcNow;

        var marcou = await Repo(ctx).MarcarLidaAsync(notif.Id, contaId, agora);

        marcou.Should().BeTrue();
        await using var verifica = fixture.CreateContext();
        var persisted = await verifica.Notificacoes.FindAsync(notif.Id);
        persisted!.Lida.Should().BeTrue();
        (await Repo(verifica).ContarNaoLidasAsync(contaId)).Should().Be(0);
    }

    [Fact]
    public async Task MarcarLidaAsync_OutroDono_NaoAfetaLinhaRetornaFalse()
    {
        await using var ctx = fixture.CreateContext();
        var contaId = await SeedContaAsync(ctx);
        var outraContaId = await SeedContaAsync(ctx);
        var notif = NovaNotificacao(contaId, TipoNotificacao.NovoTreino, DateTime.UtcNow);
        await Repo(ctx).AdicionarAsync(notif);

        var marcou = await Repo(ctx).MarcarLidaAsync(notif.Id, outraContaId, DateTime.UtcNow);

        marcou.Should().BeFalse();
        await using var verifica = fixture.CreateContext();
        var persisted = await verifica.Notificacoes.FindAsync(notif.Id);
        persisted!.Lida.Should().BeFalse();
    }

    [Fact]
    public async Task PurgarAntesDeAsync_RemoveAntigasMantemRecentes()
    {
        await using var ctx = fixture.CreateContext();
        var contaId = await SeedContaAsync(ctx);
        var limite = DateTime.UtcNow.AddDays(-90);

        var antiga = NovaNotificacao(contaId, TipoNotificacao.NovoTreino, limite.AddDays(-1));
        var recente = NovaNotificacao(contaId, TipoNotificacao.Reforco, limite.AddDays(1));
        await Repo(ctx).AdicionarAsync(antiga);
        await Repo(ctx).AdicionarAsync(recente);

        var removidas = await Repo(ctx).PurgarAntesDeAsync(limite);

        removidas.Should().Be(1);
        await using var verifica = fixture.CreateContext();
        (await verifica.Notificacoes.FindAsync(antiga.Id)).Should().BeNull();
        (await verifica.Notificacoes.FindAsync(recente.Id)).Should().NotBeNull();
    }
}
