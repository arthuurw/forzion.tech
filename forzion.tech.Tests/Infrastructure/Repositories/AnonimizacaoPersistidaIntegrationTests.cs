using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Persistence;

namespace forzion.tech.Tests.Infrastructure.Repositories;

// DOM-02: a flag de anonimização precisa ser PERSISTIDA, não só um campo em memória.
// Cada caso recarrega o agregado num contexto NOVO (sem identity-map) antes da 2ª chamada —
// é o único jeito de provar que o guard idempotente sobrevive ao round-trip do banco.
// Sem a coluna persistida, o reload traria Anonimizado=false e a 2ª chamada re-executaria
// (mexendo em UpdatedAt), quebrando a idempotência.
[Collection(InfrastructureTestCollection.Name)]
[Trait("Category", "Integration")]
public class AnonimizacaoPersistidaIntegrationTests(InfrastructureTestFixture fixture)
{
    private static readonly DateTime PrimeiraAnonimizacao = new(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SegundaTentativa = new(2026, 6, 2, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Aluno_Anonimizar_ReloadDoBanco_SegundaChamadaEhNoOp()
    {
        Guid alunoId;
        await using (var ctx = fixture.CreateContext())
        {
            var conta = Conta.Criar(Email.Criar($"a{Guid.NewGuid():N}@test.com").Value, "hash", TipoConta.Aluno, DateTime.UtcNow).Value;
            var aluno = Aluno.Criar(conta.Id, "João da Silva", DateTime.UtcNow, email: "joao@email.com", telefone: "11999999999", doencas: "Hipertensão").Value;
            await ctx.Contas.AddAsync(conta);
            await ctx.Alunos.AddAsync(aluno);
            await ctx.SaveChangesAsync();
            alunoId = aluno.Id;
        }

        await using (var ctx = fixture.CreateContext())
        {
            var aluno = await ctx.Alunos.FindAsync(alunoId);
            aluno!.Anonimizar(PrimeiraAnonimizacao);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var aluno = await ctx.Alunos.FindAsync(alunoId);
            aluno!.Anonimizado.Should().BeTrue();

            var resultado = aluno.Anonimizar(SegundaTentativa);

            resultado.IsSuccess.Should().BeTrue();
            aluno.UpdatedAt.Should().Be(PrimeiraAnonimizacao);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var aluno = await ctx.Alunos.FindAsync(alunoId);
            aluno!.Anonimizado.Should().BeTrue();
            aluno.Nome.Should().Be("Usuário anonimizado");
            aluno.Email.Should().BeNull();
            aluno.Telefone.Should().BeNull();
            aluno.Doencas.Should().BeNull();
            aluno.UpdatedAt.Should().Be(PrimeiraAnonimizacao);
        }
    }

    [Fact]
    public async Task Treinador_Anonimizar_ReloadDoBanco_SegundaChamadaEhNoOp()
    {
        Guid treinadorId;
        await using (var ctx = fixture.CreateContext())
        {
            var conta = Conta.Criar(Email.Criar($"t{Guid.NewGuid():N}@test.com").Value, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;
            var treinador = Treinador.Criar(conta.Id, "Carlos Personal", DateTime.UtcNow, telefone: "11988887777").Value;
            await ctx.Contas.AddAsync(conta);
            await ctx.Treinadores.AddAsync(treinador);
            await ctx.SaveChangesAsync();
            treinadorId = treinador.Id;
        }

        await using (var ctx = fixture.CreateContext())
        {
            var treinador = await ctx.Treinadores.FindAsync(treinadorId);
            treinador!.Anonimizar(PrimeiraAnonimizacao);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var treinador = await ctx.Treinadores.FindAsync(treinadorId);
            treinador!.Anonimizado.Should().BeTrue();

            var resultado = treinador.Anonimizar(SegundaTentativa);

            resultado.IsSuccess.Should().BeTrue();
            treinador.UpdatedAt.Should().Be(PrimeiraAnonimizacao);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = fixture.CreateContext())
        {
            var treinador = await ctx.Treinadores.FindAsync(treinadorId);
            treinador!.Anonimizado.Should().BeTrue();
            treinador.Nome.Should().Be("Usuário anonimizado");
            treinador.Telefone.Should().BeNull();
            treinador.UpdatedAt.Should().Be(PrimeiraAnonimizacao);
        }
    }
}
