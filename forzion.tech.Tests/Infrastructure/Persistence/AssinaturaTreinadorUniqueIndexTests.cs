using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace forzion.tech.Tests.Infrastructure.Persistence;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class AssinaturaTreinadorUniqueIndexTests(InfrastructureTestFixture fixture)
{
    [Fact]
    public async Task DuasAssinaturasNaoCanceladas_MesmoTreinador_ViolaUniqueIndex()
    {
        var (treinadorId, planoId) = await SeedTreinadorComPlanoAsync();
        var now = DateTime.UtcNow;

        await using var ctx1 = fixture.CreateContext();
        ctx1.AssinaturasTreinador.Add(AssinaturaTreinador.Criar(treinadorId, planoId, 50m, now).Value);
        await ctx1.SaveChangesAsync();

        await using var ctx2 = fixture.CreateContext();
        ctx2.AssinaturasTreinador.Add(AssinaturaTreinador.Criar(treinadorId, planoId, 50m, now.AddSeconds(1)).Value);
        var act = async () => await ctx2.SaveChangesAsync();

        var excecao = await act.Should().ThrowAsync<DbUpdateException>(
            "o índice único parcial deve bloquear a 2ª assinatura não-cancelada para o mesmo treinador");
        new NpgsqlDatabaseErrorInspector()
            .EhViolacaoDeUnicidade(excecao.Which)
            .Should().BeTrue("SqlState 23505 é esperado do índice ux_assinaturas_treinador_nao_cancelada_por_treinador");
    }

    [Fact]
    public async Task AssinaturaCancelada_ComAssinaturaNaoCancelada_NaoConflita()
    {
        var (treinadorId, planoId) = await SeedTreinadorComPlanoAsync();
        var now = DateTime.UtcNow;

        await using var ctx1 = fixture.CreateContext();
        ctx1.AssinaturasTreinador.Add(AssinaturaTreinador.Criar(treinadorId, planoId, 50m, now).Value);
        await ctx1.SaveChangesAsync();

        await using var ctx2 = fixture.CreateContext();
        var cancelada = AssinaturaTreinador.Criar(treinadorId, planoId, 50m, now.AddSeconds(1)).Value;
        cancelada.Cancelar(now.AddSeconds(1));
        ctx2.AssinaturasTreinador.Add(cancelada);
        var act = async () => await ctx2.SaveChangesAsync();

        await act.Should().NotThrowAsync("assinatura Cancelada está excluída do filtro do índice único parcial");
    }

    private async Task<(Guid treinadorId, Guid planoId)> SeedTreinadorComPlanoAsync()
    {
        await using var ctx = fixture.CreateContext();
        var now = DateTime.UtcNow;

        var conta = Conta.Criar(Email.Criar($"t{Guid.NewGuid():N}@test.com").Value, "hash", TipoConta.Treinador, now).Value;
        var treinador = Treinador.Criar(conta.Id, $"Tr{Guid.NewGuid():N}", now).Value;
        var plano = PlanoPlataforma.Criar($"Pl{Guid.NewGuid():N}", TierPlano.Pro, 50, 50m, now).Value;

        ctx.Contas.Add(conta);
        ctx.Treinadores.Add(treinador);
        ctx.PlanosPlataforma.Add(plano);
        await ctx.SaveChangesAsync();

        return (treinador.Id, plano.Id);
    }
}
