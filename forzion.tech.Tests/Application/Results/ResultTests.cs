using FluentAssertions;
using forzion.tech.Domain.Shared;

namespace forzion.tech.Tests.Application.Results;

public class ResultTests
{
    [Fact]
    public void Value_ResultadoFalho_LancaInvalidOperationException()
    {
        var result = Result.Failure<string>(Error.Business("test.falha", "falha"));
        var act = () => result.Value;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Value_ResultadoSucesso_RetornaValor()
    {
        var result = Result.Success("ok");
        result.Value.Should().Be("ok");
    }
}
