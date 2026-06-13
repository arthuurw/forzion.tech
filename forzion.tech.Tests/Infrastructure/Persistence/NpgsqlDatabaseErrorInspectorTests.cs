using FluentAssertions;
using forzion.tech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace forzion.tech.Tests.Infrastructure.Persistence;

public class NpgsqlDatabaseErrorInspectorTests
{
    private readonly NpgsqlDatabaseErrorInspector _inspector = new();

    // PostgresException ctor: (messageText, severity, invariantSeverity, sqlState).
    private static PostgresException Pg(string sqlState) =>
        new("dup", "ERROR", "ERROR", sqlState);

    [Fact]
    public void EhViolacaoDeUnicidade_PostgresExceptionCru_23505_True() =>
        _inspector.EhViolacaoDeUnicidade(Pg(PostgresErrorCodes.UniqueViolation)).Should().BeTrue();

    [Fact]
    public void EhViolacaoDeUnicidade_DbUpdateEnvolvendo23505_True() =>
        _inspector.EhViolacaoDeUnicidade(
            new DbUpdateException("save falhou", Pg(PostgresErrorCodes.UniqueViolation))).Should().BeTrue();

    [Fact]
    public void EhViolacaoDeUnicidade_OutroSqlState_False() =>
        _inspector.EhViolacaoDeUnicidade(Pg(PostgresErrorCodes.SerializationFailure)).Should().BeFalse();

    [Fact]
    public void EhViolacaoDeUnicidade_DbUpdateSemPostgresInner_False() =>
        _inspector.EhViolacaoDeUnicidade(
            new DbUpdateException("save falhou", new InvalidOperationException())).Should().BeFalse();

    [Fact]
    public void EhViolacaoDeUnicidade_ExcecaoGenerica_False() =>
        _inspector.EhViolacaoDeUnicidade(new InvalidOperationException("23505 na mensagem")).Should().BeFalse();
}
