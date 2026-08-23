using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Tests.Infrastructure;

// Round-trip real via Postgres (Testcontainers): grava fuso, política de agenda, capacidade de
// pacote e bloqueio de agenda em contextos distintos e relê num terceiro — precedente
// PerfilPublicoRoundTripTests (fatia 1, AD-018): tabela/coluna via SQL cru não prova Add() real.
[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class AgendaFusoCapacidadeRoundTripTests(InfrastructureTestFixture fixture)
{
    private async Task<(string ConnectionString, Guid TreinadorId, Guid PacoteId)> SeedTreinadorEPacoteAsync()
    {
        var connectionString = await fixture.CriarBancoIsoladoAsync();
        Guid treinadorId, pacoteId;

        await using var db = fixture.CreateContext(connectionString);
        var email = Email.Criar($"t{Guid.NewGuid():N}@teste.com").Value;
        var conta = Conta.Criar(email, "hash", TipoConta.Treinador, DateTime.UtcNow, emitirRegistro: false).Value;
        db.Contas.Add(conta);

        var treinador = Treinador.Criar(conta.Id, "Treinador Agenda Round Trip", DateTime.UtcNow).Value;
        db.Treinadores.Add(treinador);
        treinadorId = treinador.Id;

        var pacote = Pacote.Criar(treinador.Id, "Pacote Round Trip", 100m, DateTime.UtcNow).Value;
        db.Pacotes.Add(pacote);
        pacoteId = pacote.Id;

        await db.SaveChangesAsync();

        return (connectionString, treinadorId, pacoteId);
    }

    [Fact]
    public async Task GravarFusoPoliticaCapacidadeEBloqueio_PersisteEReleEmContextosDistintos()
    {
        var (connectionString, treinadorId, pacoteId) = await SeedTreinadorEPacoteAsync();
        var agora = DateTime.UtcNow;

        await using (var db = fixture.CreateContext(connectionString))
        {
            var treinador = await db.Treinadores.FindAsync(treinadorId);
            treinador!.DefinirFusoHorario("America/Manaus", agora);
            treinador.DefinirPoliticaAgenda(PoliticaAgenda.Criar(3, 45).Value, agora);

            var pacote = await db.Pacotes.FindAsync(pacoteId);
            pacote!.AtualizarCatalogoPublico("Categoria X", 60, null, agora, capacidadeMaxima: 5);

            await db.SaveChangesAsync();
        }

        var inicioUtc = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var fimUtc = new DateTime(2026, 6, 1, 14, 0, 0, DateTimeKind.Utc);

        await using (var db = fixture.CreateContext(connectionString))
        {
            var bloqueio = BloqueioAgenda.CriarPontual(treinadorId, inicioUtc, fimUtc, "Consulta médica", agora).Value;
            db.Set<BloqueioAgenda>().Add(bloqueio);
            await db.SaveChangesAsync();
        }

        await using var verificacao = fixture.CreateContext(connectionString);

        var treinadorRecarregado = await verificacao.Treinadores.FindAsync(treinadorId);
        treinadorRecarregado!.FusoHorario.Should().Be("America/Manaus");
        treinadorRecarregado.PoliticaAgenda.AntecedenciaMinimaHoras.Should().Be(3);
        treinadorRecarregado.PoliticaAgenda.HorizonteDias.Should().Be(45);

        var pacoteRecarregado = await verificacao.Pacotes.FindAsync(pacoteId);
        pacoteRecarregado!.CapacidadeMaxima.Should().Be(5);

        var bloqueioRecarregado = await verificacao.Set<BloqueioAgenda>()
            .SingleAsync(b => b.TreinadorId == treinadorId);
        bloqueioRecarregado.Tipo.Should().Be(TipoBloqueio.Pontual);
        bloqueioRecarregado.InicioUtc.Should().Be(inicioUtc);
        bloqueioRecarregado.FimUtc.Should().Be(fimUtc);
        bloqueioRecarregado.Motivo.Should().Be("Consulta médica");
    }
}
