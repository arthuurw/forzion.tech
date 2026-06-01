using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.AssinaturaAlunos.CancelarMinhaAssinaturaAluno;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Events;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.AssinaturaAlunos;

public class CancelarMinhaAssinaturaAlunoHandlerTests
{
    private readonly Mock<IAssinaturaAlunoRepository> _assinaturaRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<CancelarMinhaAssinaturaAlunoHandler>> _logger = new();
    private readonly CancelarMinhaAssinaturaAlunoHandler _handler;

    private static readonly Guid AlunoId = TestData.NextGuid();

    public CancelarMinhaAssinaturaAlunoHandlerTests()
    {
        _handler = new CancelarMinhaAssinaturaAlunoHandler(
            _assinaturaRepo.Object, _unitOfWork.Object, TimeProvider.System, _logger.Object);
    }

    private static AssinaturaAluno CriarAtiva()
    {
        var a = new AssinaturaAlunoBuilder().ComAlunoId(AlunoId).Build();
        a.Ativar(TestData.Agora);
        a.ClearDomainEvents();
        return a;
    }

    [Fact]
    public async Task HandleAsync_AssinaturaAtiva_CancelaECommita()
    {
        var assinatura = CriarAtiva();
        _assinaturaRepo.Setup(r => r.ObterAtualPorAlunoAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(new CancelarMinhaAssinaturaAlunoCommand(AlunoId));

        result.IsSuccess.Should().BeTrue();
        assinatura.Status.Should().Be(forzion.tech.Domain.Enums.AssinaturaAlunoStatus.Cancelada);
        assinatura.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<AssinaturaAlunoCanceladaEvent>();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SemAssinatura_RetornaFailureComCodigoNaoEncontrada()
    {
        _assinaturaRepo.Setup(r => r.ObterAtualPorAlunoAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssinaturaAluno?)null);

        var result = await _handler.HandleAsync(new CancelarMinhaAssinaturaAlunoCommand(AlunoId));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(CancelarMinhaAssinaturaAlunoHandler.AssinaturaNaoEncontradaErrorCode);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AssinaturaJaCancelada_RetornaFailureSemCommit()
    {
        var assinatura = new AssinaturaAlunoBuilder().ComAlunoId(AlunoId).Build();
        assinatura.Ativar(TestData.Agora);
        assinatura.Cancelar(TestData.Agora);
        _assinaturaRepo.Setup(r => r.ObterAtualPorAlunoAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(new CancelarMinhaAssinaturaAlunoCommand(AlunoId));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(CancelarMinhaAssinaturaAlunoHandler.AssinaturaNaoEncontradaErrorCode);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AssinaturaInadimplente_PermiteCancelar()
    {
        var assinatura = new AssinaturaAlunoBuilder().ComAlunoId(AlunoId).Build();
        assinatura.Ativar(TestData.Agora);
        assinatura.MarcarInadimplente(TestData.Agora);
        assinatura.ClearDomainEvents();
        _assinaturaRepo.Setup(r => r.ObterAtualPorAlunoAsync(AlunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(new CancelarMinhaAssinaturaAlunoCommand(AlunoId));

        result.IsSuccess.Should().BeTrue();
        assinatura.Status.Should().Be(forzion.tech.Domain.Enums.AssinaturaAlunoStatus.Cancelada);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
