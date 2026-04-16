using FluentAssertions;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Tests.Domain.Exceptions;

public class DomainExceptionTests
{
    [Fact]
    public void DomainException_Default_CriaSemMensagem()
    {
        var ex = new DomainException();
        ex.Should().BeOfType<DomainException>();
    }

    [Fact]
    public void DomainException_ComMensagem_CriaComMensagem()
    {
        var ex = new DomainException("erro");
        ex.Message.Should().Be("erro");
    }

    [Fact]
    public void DomainException_ComMensagemEInner_CriaComAmbos()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new DomainException("outer", inner);
        ex.Message.Should().Be("outer");
        ex.InnerException.Should().Be(inner);
    }

    [Fact]
    public void DomainException_EHeritagemDeException()
    {
        new DomainException().Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void AlunoNaoEncontradoException_Default_MensagemCorreta()
    {
        var ex = new AlunoNaoEncontradoException();
        ex.Message.Should().Be("Aluno não encontrado.");
        ex.Should().BeAssignableTo<DomainException>();
    }

    [Fact]
    public void AlunoInativoException_Default_MensagemCorreta()
    {
        var ex = new AlunoInativoException();
        ex.Message.Should().Be("Aluno inativo.");
        ex.Should().BeAssignableTo<DomainException>();
    }

    [Fact]
    public void TreinoNaoEncontradoException_Default_MensagemCorreta()
    {
        var ex = new TreinoNaoEncontradoException();
        ex.Message.Should().Be("Treino não encontrado.");
        ex.Should().BeAssignableTo<DomainException>();
    }

    [Fact]
    public void TreinoExecutadoException_Default_MensagemCorreta()
    {
        var ex = new TreinoExecutadoException();
        ex.Message.Should().Be("Treino já executado não pode ser alterado.");
        ex.Should().BeAssignableTo<DomainException>();
    }

    [Fact]
    public void TreinoAlunoNaoEncontradoException_NaoExiste_TreinoNaoEncontradoUsado()
    {
        // TreinoNaoEncontradoException cobre ausência de treino e de vínculo
        var ex = new TreinoNaoEncontradoException();
        ex.Should().BeAssignableTo<DomainException>();
    }

    [Fact]
    public void ExercicioNaoEncontradoException_Default_MensagemCorreta()
    {
        var ex = new ExercicioNaoEncontradoException();
        ex.Message.Should().Be("Exercício não encontrado.");
        ex.Should().BeAssignableTo<DomainException>();
    }
}
