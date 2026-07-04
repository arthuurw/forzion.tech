using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Events;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Domain.Entities;

public class ExecucaoTreinoTests
{
    private static ExecucaoTreino CriarExecucao() =>
        ExecucaoTreino.Criar(Guid.NewGuid(), Guid.NewGuid(), TestData.Agora, TestData.Agora).Value;

    // --- Criar ---

    [Fact]
    public void Criar_DadosValidos_RetornaExecucao()
    {
        var e = CriarExecucao();
        e.Exercicios.Should().BeEmpty();
        e.Observacao.Should().BeNull();
    }

    [Fact]
    public void Criar_DadosValidos_EmiteExecucaoRegistradaEventComIds()
    {
        var treinoId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();

        var e = ExecucaoTreino.Criar(treinoId, alunoId, TestData.Agora, TestData.Agora).Value;

        var evento = e.DomainEvents.OfType<ExecucaoRegistradaEvent>().Should().ContainSingle().Subject;
        evento.AlunoId.Should().Be(alunoId);
        evento.TreinoId.Should().Be(treinoId);
        evento.ExecucaoTreinoId.Should().Be(e.Id);
        evento.OcorridoEm.Should().Be(TestData.Agora);
    }

    [Fact]
    public void ClearDomainEvents_RemoveEventoEmitido()
    {
        var e = CriarExecucao();

        e.ClearDomainEvents();

        e.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Criar_TreinoIdVazio_LancaDomainException()
    {
        var r = ExecucaoTreino.Criar(Guid.Empty, Guid.NewGuid(), TestData.Agora, TestData.Agora);
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Criar_AlunoIdVazio_LancaDomainException()
    {
        var r = ExecucaoTreino.Criar(Guid.NewGuid(), Guid.Empty, TestData.Agora, TestData.Agora);
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Criar_DataDefault_LancaDomainException()
    {
        var r = ExecucaoTreino.Criar(Guid.NewGuid(), Guid.NewGuid(), default, TestData.Agora);
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Criar_ObservacaoMuitoLonga_LancaDomainException()
    {
        var r = ExecucaoTreino.Criar(
            Guid.NewGuid(), Guid.NewGuid(), TestData.Agora, TestData.Agora, new string('a', 501));
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Criar_SemIdempotencyKey_KeyNula()
    {
        var e = CriarExecucao();
        e.IdempotencyKey.Should().BeNull();
    }

    [Fact]
    public void Criar_ComIdempotencyKey_PersisteKey()
    {
        var key = Guid.NewGuid().ToString();
        var e = ExecucaoTreino.Criar(
            Guid.NewGuid(), Guid.NewGuid(), TestData.Agora, TestData.Agora, null, key).Value;
        e.IdempotencyKey.Should().Be(key);
    }

    // --- AdicionarExercicio ---

    [Fact]
    public void AdicionarExercicio_DadosValidos_Adiciona()
    {
        var e = CriarExecucao();
        e.AdicionarExercicio(Guid.NewGuid(), 3, 12, 10m);
        e.Exercicios.Should().HaveCount(1);
        e.Exercicios[0].SeriesExecutadas.Should().Be(3);
        e.Exercicios[0].RepeticoesExecutadas.Should().Be(12);
        e.Exercicios[0].CargaExecutada.Should().Be(10m);
    }

    [Fact]
    public void AdicionarExercicio_SeriesZero_LancaDomainException()
    {
        var e = CriarExecucao();
        var r = e.AdicionarExercicio(Guid.NewGuid(), 0, 12, null);
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AdicionarExercicio_RepeticoesZero_LancaDomainException()
    {
        var e = CriarExecucao();
        var r = e.AdicionarExercicio(Guid.NewGuid(), 3, 0, null);
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AdicionarExercicio_CargaNegativa_LancaDomainException()
    {
        var e = CriarExecucao();
        var r = e.AdicionarExercicio(Guid.NewGuid(), 3, 12, -1m);
        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AdicionarExercicio_MultiplosPorExecucao()
    {
        var e = CriarExecucao();
        e.AdicionarExercicio(Guid.NewGuid(), 3, 12, null);
        e.AdicionarExercicio(Guid.NewGuid(), 4, 8, 20m);
        e.Exercicios.Should().HaveCount(2);
    }

    [Fact]
    public void AdicionarExercicio_TreinoExercicioIdVazio_LancaDomainException()
    {
        var e = CriarExecucao();
        var r = e.AdicionarExercicio(Guid.Empty, 3, 12, null);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O exercício do treino é inválido.");
    }

    [Fact]
    public void AdicionarExercicio_ObservacaoMuitoLonga_LancaDomainException()
    {
        var e = CriarExecucao();
        var r = e.AdicionarExercicio(Guid.NewGuid(), 3, 12, null, new string('a', 501));
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("A observação deve ter no máximo 500 caracteres.");
    }

    [Fact]
    public void AdicionarExercicio_ComObservacao_SalvaObservacao()
    {
        var e = CriarExecucao();
        e.AdicionarExercicio(Guid.NewGuid(), 3, 12, null, "Foco na contração");

        e.Exercicios[0].Observacao.Should().Be("Foco na contração");
    }
}
