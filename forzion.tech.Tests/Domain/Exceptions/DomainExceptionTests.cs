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
    public void UsuarioJaRegistradoException_Default_MensagemCorreta()
    {
        var ex = new UsuarioJaRegistradoException();
        ex.Message.Should().Be("Usuário já registrado.");
    }

    [Fact]
    public void UsuarioJaRegistradoException_ComMensagem_UsaMensagem()
    {
        var ex = new UsuarioJaRegistradoException("mensagem customizada");
        ex.Message.Should().Be("mensagem customizada");
    }

    [Fact]
    public void UsuarioJaRegistradoException_ComInner_PropagaInner()
    {
        var inner = new Exception("causa");
        var ex = new UsuarioJaRegistradoException("msg", inner);
        ex.InnerException.Should().Be(inner);
    }

    [Fact]
    public void UsuarioNaoEncontradoException_Default_MensagemCorreta()
    {
        var ex = new UsuarioNaoEncontradoException();
        ex.Message.Should().Be("Usuário não encontrado.");
    }

    [Fact]
    public void UsuarioNaoEncontradoException_ComMensagem_UsaMensagem()
    {
        var ex = new UsuarioNaoEncontradoException("mensagem customizada");
        ex.Message.Should().Be("mensagem customizada");
    }

    [Fact]
    public void UsuarioNaoEncontradoException_ComInner_PropagaInner()
    {
        var inner = new Exception("causa");
        var ex = new UsuarioNaoEncontradoException("msg", inner);
        ex.InnerException.Should().Be(inner);
    }

    [Fact]
    public void UsuarioInativoException_Default_MensagemCorreta()
    {
        var ex = new UsuarioInativoException();
        ex.Message.Should().Be("Usuário inativo.");
    }

    [Fact]
    public void PlanoNaoEncontradoException_Default_MensagemCorreta()
    {
        var ex = new PlanoNaoEncontradoException();
        ex.Message.Should().Be("Plano Free não encontrado.");
    }

    [Fact]
    public void PlanoNaoEncontradoException_ComMensagem_UsaMensagem()
    {
        var ex = new PlanoNaoEncontradoException("msg custom");
        ex.Message.Should().Be("msg custom");
    }

    [Fact]
    public void PlanoNaoEncontradoException_ComInner_PropagaInner()
    {
        var inner = new Exception("causa");
        var ex = new PlanoNaoEncontradoException("msg", inner);
        ex.InnerException.Should().Be(inner);
    }

    [Fact]
    public void DomainException_EHeritagemDeException()
    {
        new DomainException().Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void UsuarioJaRegistradoException_EHeritagemDeDomainException()
    {
        new UsuarioJaRegistradoException().Should().BeAssignableTo<DomainException>();
    }
}
