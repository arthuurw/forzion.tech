using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace forzion.tech.Tests.Infrastructure.Repositories;

[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class CheckConstraintsIntegridadeTests(InfrastructureTestFixture fixture)
{
    private static async Task AssertCheckViolationAsync(Func<Task> act) =>
        (await act.Should().ThrowAsync<PostgresException>())
            .Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);

    [Fact]
    public async Task InserirPlano_PrecoNegativo_ViolaCheck()
    {
        await using var ctx = fixture.CreateContext();

        await AssertCheckViolationAsync(() => ctx.Database.ExecuteSqlRawAsync(
            "INSERT INTO planos_plataforma (id, nome, tier, max_alunos, preco, is_ativo, created_at) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6})",
            Guid.NewGuid(), $"P{Guid.NewGuid():N}", nameof(TierPlano.Free), 10, -1m, true, DateTime.UtcNow));
    }

    [Fact]
    public async Task InserirPlano_MaxAlunosZero_ViolaCheck()
    {
        await using var ctx = fixture.CreateContext();

        await AssertCheckViolationAsync(() => ctx.Database.ExecuteSqlRawAsync(
            "INSERT INTO planos_plataforma (id, nome, tier, max_alunos, preco, is_ativo, created_at) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6})",
            Guid.NewGuid(), $"P{Guid.NewGuid():N}", nameof(TierPlano.Free), 0, 50m, true, DateTime.UtcNow));
    }

    [Fact]
    public async Task InserirPlano_Valido_Persiste()
    {
        await using var ctx = fixture.CreateContext();
        var plano = PlanoPlataforma.Criar($"P{Guid.NewGuid():N}", TierPlano.Free, 10, 0m, DateTime.UtcNow).Value;

        ctx.PlanosPlataforma.Add(plano);
        var act = async () => await ctx.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AtualizarAssinatura_ValorNegativo_ViolaCheck()
    {
        await using var ctx = fixture.CreateContext();
        var assinaturaId = await SeedAssinaturaAsync(ctx);

        await AssertCheckViolationAsync(() => ctx.Database.ExecuteSqlRawAsync(
            "UPDATE assinaturas_aluno SET valor = -1 WHERE id = {0}", assinaturaId));
    }

    [Fact]
    public async Task AtualizarSerie_QuantidadeZero_ViolaCheck()
    {
        await using var ctx = fixture.CreateContext();
        var serieId = await SeedSerieAsync(ctx);

        await AssertCheckViolationAsync(() => ctx.Database.ExecuteSqlRawAsync(
            "UPDATE treino_exercicio_series SET quantidade = 0 WHERE id = {0}", serieId));
    }

    [Fact]
    public async Task AtualizarSerie_RepeticoesMaxMenorQueMin_ViolaCheck()
    {
        await using var ctx = fixture.CreateContext();
        var serieId = await SeedSerieAsync(ctx);

        await AssertCheckViolationAsync(() => ctx.Database.ExecuteSqlRawAsync(
            "UPDATE treino_exercicio_series SET repeticoes_max = 1 WHERE id = {0}", serieId));
    }

    private static async Task<Guid> SeedAssinaturaAsync(AppDbContext ctx)
    {
        var now = DateTime.UtcNow;
        var contaT = Conta.Criar(Email.Criar($"t{Guid.NewGuid():N}@test.com").Value, "hash", TipoConta.Treinador, now).Value;
        var treinador = Treinador.Criar(contaT.Id, $"Tr{Guid.NewGuid():N}", now).Value;
        var contaA = Conta.Criar(Email.Criar($"a{Guid.NewGuid():N}@test.com").Value, "hash", TipoConta.Aluno, now).Value;
        var aluno = Aluno.Criar(contaA.Id, $"Al{Guid.NewGuid():N}", now).Value;
        var pacote = Pacote.Criar(treinador.Id, $"Pac{Guid.NewGuid():N}", 99.90m, now).Value;
        var vinculo = VinculoTreinadorAluno.Criar(treinador.Id, aluno.Id, now).Value;
        vinculo.Aprovar(treinador.Id, pacote.Id, now);
        var assinatura = AssinaturaAluno.Criar(vinculo.Id, pacote.Id, treinador.Id, aluno.Id, 99.90m, now).Value;

        ctx.Contas.AddRange(contaT, contaA);
        ctx.Treinadores.Add(treinador);
        ctx.Alunos.Add(aluno);
        ctx.Pacotes.Add(pacote);
        ctx.VinculosTreinadorAluno.Add(vinculo);
        ctx.AssinaturaAlunos.Add(assinatura);
        await ctx.SaveChangesAsync();
        return assinatura.Id;
    }

    private static async Task<Guid> SeedSerieAsync(AppDbContext ctx)
    {
        var now = DateTime.UtcNow;
        var contaT = Conta.Criar(Email.Criar($"t{Guid.NewGuid():N}@test.com").Value, "hash", TipoConta.Treinador, now).Value;
        var treinador = Treinador.Criar(contaT.Id, $"Tr{Guid.NewGuid():N}", now).Value;
        var grupo = GrupoMuscular.Criar($"G{Guid.NewGuid():N}"[..30], now).Value;
        var exercicio = Exercicio.Criar($"Ex{Guid.NewGuid():N}", grupo.Id, now).Value;
        var treino = Treino.Criar($"T{Guid.NewGuid():N}", ObjetivoTreino.Hipertrofia, treinador.Id, now).Value;
        var treinoExercicio = treino.AdicionarExercicio(exercicio.Id, now).Value;
        treinoExercicio.AdicionarSerie(3, 8, 12, null, null, null);

        ctx.Contas.Add(contaT);
        ctx.Treinadores.Add(treinador);
        ctx.GruposMusculares.Add(grupo);
        ctx.Exercicios.Add(exercicio);
        ctx.Treinos.Add(treino);
        await ctx.SaveChangesAsync();
        return treinoExercicio.Series[0].Id;
    }
}
