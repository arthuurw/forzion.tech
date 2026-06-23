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

    [Fact]
    public void EhConflitoDeSerializacao_PostgresExceptionCru_40001_True() =>
        _inspector.EhConflitoDeSerializacao(Pg(PostgresErrorCodes.SerializationFailure)).Should().BeTrue();

    [Fact]
    public void EhConflitoDeSerializacao_DbUpdateEnvolvendo40001_True() =>
        _inspector.EhConflitoDeSerializacao(
            new DbUpdateException("save falhou", Pg(PostgresErrorCodes.SerializationFailure))).Should().BeTrue();

    [Fact]
    public void EhConflitoDeSerializacao_ExecutionStrategyEnvolvendo40001_True() =>
        _inspector.EhConflitoDeSerializacao(
            new InvalidOperationException("transient failure",
                new DbUpdateException("save falhou", Pg(PostgresErrorCodes.SerializationFailure)))).Should().BeTrue();

    [Fact]
    public void EhConflitoDeSerializacao_DeadlockDetected_True() =>
        _inspector.EhConflitoDeSerializacao(Pg(PostgresErrorCodes.DeadlockDetected)).Should().BeTrue();

    [Fact]
    public void EhConflitoDeSerializacao_DbUpdateConcurrencyException_True() =>
        _inspector.EhConflitoDeSerializacao(new DbUpdateConcurrencyException("conflito")).Should().BeTrue();

    [Fact]
    public void EhConflitoDeSerializacao_OutroSqlState_False() =>
        _inspector.EhConflitoDeSerializacao(Pg(PostgresErrorCodes.UniqueViolation)).Should().BeFalse();

    [Fact]
    public void EhConflitoDeSerializacao_ExcecaoGenerica_False() =>
        _inspector.EhConflitoDeSerializacao(new InvalidOperationException("sem causa pg")).Should().BeFalse();

    [Fact]
    public void EhConflitoDeConcorrenciaOtimista_DbUpdateConcurrencyExceptionCru_True() =>
        _inspector.EhConflitoDeConcorrenciaOtimista(new DbUpdateConcurrencyException("xmin")).Should().BeTrue();

    [Fact]
    public void EhConflitoDeConcorrenciaOtimista_Reembrulhado_True() =>
        _inspector.EhConflitoDeConcorrenciaOtimista(
            new InvalidOperationException("transient failure",
                new DbUpdateConcurrencyException("xmin"))).Should().BeTrue();

    [Fact]
    public void EhConflitoDeConcorrenciaOtimista_SerializationFailure_False() =>
        _inspector.EhConflitoDeConcorrenciaOtimista(Pg(PostgresErrorCodes.SerializationFailure)).Should().BeFalse();

    [Fact]
    public void EhConflitoDeConcorrenciaOtimista_ExcecaoGenerica_False() =>
        _inspector.EhConflitoDeConcorrenciaOtimista(new InvalidOperationException("sem causa")).Should().BeFalse();
}
