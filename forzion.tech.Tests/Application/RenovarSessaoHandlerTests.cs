using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Auth.RenovarSessao;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application;

public class RenovarSessaoHandlerTests
{
    private readonly Mock<IRefreshTokenService> _refresh = new();
    private readonly Mock<IJwtService> _jwt = new();
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<ISystemUserRepository> _systemUserRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ILogger<RenovarSessaoHandler>> _logger = new();
    private readonly RenovarSessaoHandler _handler;

    public RenovarSessaoHandlerTests()
    {
        _handler = new RenovarSessaoHandler(
            _refresh.Object, _jwt.Object, _alunoRepo.Object, _treinadorRepo.Object,
            _systemUserRepo.Object, _uow.Object,
            new FakeTimeProvider(new DateTimeOffset(new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc))),
            _logger.Object);
    }

    private static Conta NovaConta(TipoConta tipo) =>
        Conta.Criar(Email.Criar("a@test.com").Value, "hash", tipo, DateTime.UtcNow).Value;

    [Fact]
    public async Task Renovar_TokenValido_AlunoReemiteAccessERefresh()
    {
        var conta = NovaConta(TipoConta.Aluno);
        var aluno = Aluno.Criar(conta.Id, "João Aluno", DateTime.UtcNow).Value;
        _refresh.Setup(s => s.RotacionarAsync("raw", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RotacaoResultado.Sucesso(conta, Guid.NewGuid(), "novoraw"));
        _alunoRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);
        _jwt.Setup(j => j.GerarToken(conta, aluno.Id, "João Aluno", It.IsAny<Guid>())).Returns("access.jwt");

        var result = await _handler.HandleAsync(new RenovarSessaoCommand("raw"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Token.Should().Be("access.jwt");
        result.Value.RefreshToken.Should().Be("novoraw");
        result.Value.Nome.Should().Be("João Aluno");
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Renovar_ReuseDetectado_FalhaECommitaRevogacaoELogaWarning()
    {
        _refresh.Setup(s => s.RotacionarAsync("raw", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RotacaoResultado.Reuse(Guid.NewGuid()));

        var result = await _handler.HandleAsync(new RenovarSessaoCommand("raw"));

        result.IsFailure.Should().BeTrue();
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _jwt.Verify(j => j.GerarToken(It.IsAny<Conta>(), It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
        _logger.Verify(l => l.Log(LogLevel.Warning, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("família") || v.ToString()!.Contains("reuso") || v.ToString()!.Contains("Reuso")),
            null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task Renovar_Invalido_FalhaSemCommitELogaWarning()
    {
        _refresh.Setup(s => s.RotacionarAsync("raw", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RotacaoResultado.Invalido());

        var result = await _handler.HandleAsync(new RenovarSessaoCommand("raw"));

        result.IsFailure.Should().BeTrue();
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _logger.Verify(l => l.Log(LogLevel.Warning, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("inválido") || v.ToString()!.Contains("negada")),
            null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task Renovar_TreinadorInativo_FalhaNaoReemite()
    {
        var conta = NovaConta(TipoConta.Treinador);
        var treinador = Treinador.Criar(conta.Id, "João Trainer", DateTime.UtcNow).Value;
        treinador.Reprovar(Guid.NewGuid(), DateTime.UtcNow);
        _refresh.Setup(s => s.RotacionarAsync("raw", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RotacaoResultado.Sucesso(conta, Guid.NewGuid(), "novoraw"));
        _treinadorRepo.Setup(r => r.ObterPorContaIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var result = await _handler.HandleAsync(new RenovarSessaoCommand("raw"));

        result.IsFailure.Should().BeTrue();
        _jwt.Verify(j => j.GerarToken(It.IsAny<Conta>(), It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
