using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Outbox;
using forzion.tech.Application.UseCases.Admin.NotasFiscais;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Application.Admin.NotasFiscais;

public class ReprocessarNotaFiscalHandlerTests
{
    private static readonly DateTime Agora = new(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);

    private readonly Mock<INotaFiscalRepository> _notaRepo = new();
    private readonly Mock<IOutboxEnfileirador> _enfileirador = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogAprovacaoRepository> _logRepo = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Agora, TimeSpan.Zero));
    private readonly ReprocessarNotaFiscalHandler _handler;

    public ReprocessarNotaFiscalHandlerTests()
    {
        _userContext.Setup(u => u.PerfilId).Returns(Guid.NewGuid());
        _handler = new ReprocessarNotaFiscalHandler(
            _notaRepo.Object, _enfileirador.Object, _unitOfWork.Object,
            Mock.Of<ILogger<ReprocessarNotaFiscalHandler>>(),
            _logRepo.Object, _userContext.Object, _time);
    }

    private static NotaFiscal NotaEmErro()
    {
        var nota = NotaFiscal.CriarComissao(Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 50m, Agora).Value;
        nota.MarcarErro("E1", "falha", Agora);
        return nota;
    }

    [Fact]
    public async Task DoisReprocessos_GeramChaveIdempotenciaEstavelIdentica()
    {
        var nota = NotaEmErro();
        _notaRepo.Setup(r => r.ObterPorIdAsync(nota.Id, It.IsAny<CancellationToken>())).ReturnsAsync(nota);
        var chaves = new List<string>();
        _enfileirador
            .Setup(e => e.Enfileirar(It.IsAny<string>(), It.IsAny<EmitirNfsePayload>(), It.IsAny<string>()))
            .Callback<string, EmitirNfsePayload, string>((_, _, chave) => chaves.Add(chave));

        (await _handler.HandleAsync(nota.Id)).IsSuccess.Should().BeTrue();
        (await _handler.HandleAsync(nota.Id)).IsSuccess.Should().BeTrue();

        chaves.Should().HaveCount(2);
        chaves.Should().AllBe($"fx:emitir_nfse:reprocessar:{nota.Id}");
        _logRepo.Verify(r => r.AdicionarAsync(It.Is<LogAprovacao>(l => l.TipoAcao == TipoAcaoAprovacao.ReprocessamentoNotaFiscal), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task NotaNaoEmErro_NaoEnfileira()
    {
        var pendente = NotaFiscal.CriarComissao(Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), 50m, Agora).Value;
        _notaRepo.Setup(r => r.ObterPorIdAsync(pendente.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pendente);

        var result = await _handler.HandleAsync(pendente.Id);

        result.IsFailure.Should().BeTrue();
        _enfileirador.Verify(e => e.Enfileirar(It.IsAny<string>(), It.IsAny<EmitirNfsePayload>(), It.IsAny<string>()), Times.Never);
    }
}
