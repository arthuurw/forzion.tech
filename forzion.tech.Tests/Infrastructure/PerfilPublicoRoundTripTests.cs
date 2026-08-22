using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Tests.Infrastructure;

// Round-trip real via Postgres (Testcontainers): grava PerfilPublico+horários numa aggregate
// recém-recarregada (DbContext novo, não o mesmo que criou o Treinador) e relê num terceiro
// DbContext. Cobre o caminho que as tasks originais da fatia 1 (T7-T10) não exercitaram —
// aquelas só validavam forma de schema via SQL cru, nunca Add a uma OwnsMany após reload.
[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class PerfilPublicoRoundTripTests(InfrastructureTestFixture fixture)
{
    private async Task<(string ConnectionString, Guid TreinadorId)> SeedTreinadorAsync()
    {
        var connectionString = await fixture.CriarBancoIsoladoAsync();
        Guid treinadorId;

        await using var db = fixture.CreateContext(connectionString);
        var email = Email.Criar($"t{Guid.NewGuid():N}@teste.com").Value;
        var conta = Conta.Criar(email, "hash", TipoConta.Treinador, DateTime.UtcNow, emitirRegistro: false).Value;
        db.Contas.Add(conta);
        var treinador = Treinador.Criar(conta.Id, "Treinador Round Trip", DateTime.UtcNow).Value;
        db.Treinadores.Add(treinador);
        treinadorId = treinador.Id;
        await db.SaveChangesAsync();

        return (connectionString, treinadorId);
    }

    [Fact]
    public async Task AdicionarHorarioAposReload_PersisteComoInsertNaoUpdate()
    {
        var (connectionString, treinadorId) = await SeedTreinadorAsync();

        await using (var db = fixture.CreateContext(connectionString))
        {
            var treinador = await db.Treinadores.FindAsync(treinadorId);
            treinador!.PerfilPublico.AtualizarDados("Studio Round Trip", null, null, DateTime.UtcNow);
            treinador.PerfilPublico.AdicionarHorario(1, new TimeOnly(8, 0), new TimeOnly(18, 0), DateTime.UtcNow);
            await db.SaveChangesAsync();
        }

        await using var verificacao = fixture.CreateContext(connectionString);
        var recarregado = await verificacao.Treinadores.FindAsync(treinadorId);

        recarregado!.PerfilPublico.NomeFantasia.Should().Be("Studio Round Trip");
        recarregado.PerfilPublico.HorariosFuncionamento.Should().ContainSingle(
            h => h.DiaSemana == 1 && h.AbreAs == new TimeOnly(8, 0) && h.FechaAs == new TimeOnly(18, 0));
    }

    [Fact]
    public async Task SubstituirHorariosAposReload_TrocaPersisteEEndereco_PoliticasSalvamCorretamente()
    {
        var (connectionString, treinadorId) = await SeedTreinadorAsync();
        var endereco = EnderecoPublico.Criar("Rua das Flores", "Centro", "São Paulo", "SP", "01001000").Value;
        var politicas = new Dictionary<string, string> { ["cancelamento"] = "24h de antecedência" };

        await using (var db = fixture.CreateContext(connectionString))
        {
            var treinador = await db.Treinadores.FindAsync(treinadorId);
            treinador!.PerfilPublico.AtualizarDados("Studio X", endereco, politicas, DateTime.UtcNow);
            treinador.PerfilPublico.SubstituirHorarios(
                [(1, new TimeOnly(8, 0), new TimeOnly(12, 0)), (2, new TimeOnly(14, 0), new TimeOnly(20, 0))],
                DateTime.UtcNow);
            treinador.PerfilPublico.Publicar(DateTime.UtcNow);
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.CreateContext(connectionString))
        {
            var treinador = await db.Treinadores.FindAsync(treinadorId);
            treinador!.PerfilPublico.SubstituirHorarios([(3, new TimeOnly(9, 0), new TimeOnly(17, 0))], DateTime.UtcNow);
            await db.SaveChangesAsync();
        }

        await using var verificacao = fixture.CreateContext(connectionString);
        var recarregado = await verificacao.Treinadores.FindAsync(treinadorId);

        recarregado!.PerfilPublico.IsPublicado.Should().BeTrue();
        recarregado.PerfilPublico.Endereco.Should().NotBeNull();
        recarregado.PerfilPublico.Endereco!.Cidade.Should().Be("São Paulo");
        recarregado.PerfilPublico.Politicas.Should().BeEquivalentTo(politicas);
        recarregado.PerfilPublico.HorariosFuncionamento.Should().ContainSingle(h => h.DiaSemana == 3);
    }
}
