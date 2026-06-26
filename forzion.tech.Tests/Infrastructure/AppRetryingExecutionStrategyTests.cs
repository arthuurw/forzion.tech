using FluentAssertions;
using forzion.tech.Infrastructure.Persistence;
using Npgsql;
using Xunit;

namespace forzion.tech.Tests.Infrastructure;

public class AppRetryingExecutionStrategyTests
{
    [Theory]
    [InlineData("40001")]
    [InlineData("40P01")]
    public void EhConflitoDeSerializacao_CodigoDeSerializacaoOuDeadlock_True(string sqlState)
    {
        var ex = new PostgresException("conflito", "ERROR", "ERROR", sqlState);

        AppRetryingExecutionStrategy.EhConflitoDeSerializacao(ex).Should().BeTrue();
    }

    [Fact]
    public void EhConflitoDeSerializacao_PostgresExceptionAninhada_VarreCadeia()
    {
        var raiz = new PostgresException("conflito", "ERROR", "ERROR", "40001");
        var embrulhada = new InvalidOperationException("transient failure", new Exception("camada", raiz));

        AppRetryingExecutionStrategy.EhConflitoDeSerializacao(embrulhada).Should().BeTrue();
    }

    [Theory]
    [InlineData("23505")]
    [InlineData("08006")]
    public void EhConflitoDeSerializacao_OutroSqlState_False(string sqlState)
    {
        var ex = new PostgresException("falha", "ERROR", "ERROR", sqlState);

        AppRetryingExecutionStrategy.EhConflitoDeSerializacao(ex).Should().BeFalse();
    }

    [Fact]
    public void EhConflitoDeSerializacao_SemPostgresException_False()
    {
        AppRetryingExecutionStrategy.EhConflitoDeSerializacao(new InvalidOperationException("qualquer")).Should().BeFalse();
        AppRetryingExecutionStrategy.EhConflitoDeSerializacao(null).Should().BeFalse();
    }
}
