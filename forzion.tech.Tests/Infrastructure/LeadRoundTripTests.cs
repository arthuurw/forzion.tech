using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace forzion.tech.Tests.Infrastructure;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class LeadRoundTripTests(InfrastructureTestFixture fixture)
{
    private static readonly DateTime Agora = new(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

    private static Lead NovoLead(Guid treinadorId) =>
        Lead.Criar(
            treinadorId,
            "Fulano",
            ContatoLead.Criar(TipoContatoLead.Email, "lead@example.com").Value,
            "quero treinar",
            ConsentimentoLead.Criar("Contato comercial", Agora, Agora).Value,
            null,
            LeadSource.Agent,
            null,
            null,
            Agora).Value;

    private async Task<(string ConnectionString, Guid TreinadorId)> SeedTreinadorAsync()
    {
        var connectionString = await fixture.CriarBancoIsoladoAsync();

        await using var db = fixture.CreateContext(connectionString);
        var email = Email.Criar($"t{Guid.NewGuid():N}@teste.com").Value;
        var conta = Conta.Criar(email, "hash", TipoConta.Treinador, Agora, emitirRegistro: false).Value;
        db.Contas.Add(conta);
        var treinador = Treinador.Criar(conta.Id, "Treinador Round Trip", Agora).Value;
        db.Treinadores.Add(treinador);
        await db.SaveChangesAsync();

        return (connectionString, treinador.Id);
    }

    [Fact]
    public async Task SalvarRecarregarMutarERelerEmDbContextsDiferentes_PersisteInteracaoComoInsert()
    {
        var (connectionString, treinadorId) = await SeedTreinadorAsync();
        var lead = NovoLead(treinadorId);
        var leadId = lead.Id;

        await using (var db = fixture.CreateContext(connectionString))
        {
            db.Set<Lead>().Add(lead);
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.CreateContext(connectionString))
        {
            var recarregado = await db.Set<Lead>().Include("Interacoes").FirstAsync(l => l.Id == leadId);
            recarregado.MarcarEmContato(Guid.NewGuid(), "liguei", Agora.AddHours(1));
            await db.SaveChangesAsync();
        }

        await using var verificacao = fixture.CreateContext(connectionString);
        var final = await verificacao.Set<Lead>().Include("Interacoes").FirstAsync(l => l.Id == leadId);

        final.Status.Should().Be(LeadStatus.EmContato);
        final.Interacoes.Should().ContainSingle();
        final.Interacoes[0].Observacao.Should().Be("liguei");
        final.Nome.Should().Be("Fulano");
        final.Contato.Valor.Should().Be("lead@example.com");
    }

    [Fact]
    public async Task DoisLeadsComIdempotencyKeyNula_NaoColidem()
    {
        var (connectionString, treinadorId) = await SeedTreinadorAsync();

        await using var db = fixture.CreateContext(connectionString);
        db.Set<Lead>().Add(NovoLead(treinadorId));
        db.Set<Lead>().Add(NovoLead(treinadorId));

        var act = async () => await db.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DoisLeadsMesmoTreinadorEMesmaIdempotencyKey_ViolaIndiceUnico()
    {
        var (connectionString, treinadorId) = await SeedTreinadorAsync();

        var contato = ContatoLead.Criar(TipoContatoLead.Email, "lead@example.com").Value;
        var consentimento = ConsentimentoLead.Criar("Contato comercial", Agora, Agora).Value;
        var primeiro = Lead.Criar(treinadorId, "Fulano", contato, null, consentimento, null, LeadSource.Agent, "chave-repetida", "hash-1", Agora).Value;
        var segundo = Lead.Criar(treinadorId, "Ciclano", contato, null, consentimento, null, LeadSource.Agent, "chave-repetida", "hash-2", Agora).Value;

        await using (var db = fixture.CreateContext(connectionString))
        {
            db.Set<Lead>().Add(primeiro);
            await db.SaveChangesAsync();
        }

        await using var outro = fixture.CreateContext(connectionString);
        outro.Set<Lead>().Add(segundo);
        var act = async () => await outro.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
