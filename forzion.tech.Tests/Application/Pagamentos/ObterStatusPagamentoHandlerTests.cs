using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Pagamentos.ObterStatusPagamento;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;
using Moq;

namespace forzion.tech.Tests.Application.Pagamentos;

public class ObterStatusPagamentoHandlerTests
{
    private readonly Mock<IPagamentoRepository> _pagamentoRepo = new();
    private readonly Mock<IAssinaturaAlunoRepository> _assinaturaRepo = new();
    private readonly ObterStatusPagamentoHandler _handler;

    public ObterStatusPagamentoHandlerTests()
    {
        _handler = new ObterStatusPagamentoHandler(_pagamentoRepo.Object, _assinaturaRepo.Object);
    }

    [Fact]
    public async Task HandleAsync_DonoPagamento_RetornaResponse()
    {
        var alunoId = Guid.NewGuid();
        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), alunoId, 150m, DateTime.UtcNow).Value;
        var pagamento = Pagamento.Criar(assinatura.Id, 150m, DateTime.UtcNow).Value;

        _pagamentoRepo.Setup(r => r.ObterPorIdAsync(pagamento.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        var result = await _handler.HandleAsync(new ObterStatusPagamentoQuery(pagamento.Id, alunoId));

        result.IsSuccess.Should().BeTrue();
        result.Value.PagamentoId.Should().Be(pagamento.Id);
        result.Value.Valor.Should().Be(150m);
    }

    [Fact]
    public async Task HandleAsync_AlunoErrado_LancaAcessoNegado()
    {
        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 150m, DateTime.UtcNow).Value;
        var pagamento = Pagamento.Criar(assinatura.Id, 150m, DateTime.UtcNow).Value;

        _pagamentoRepo.Setup(r => r.ObterPorIdAsync(pagamento.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pagamento);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        var act = async () => await _handler.HandleAsync(new ObterStatusPagamentoQuery(pagamento.Id, Guid.NewGuid()));
        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_PagamentoNaoEncontrado_RetornaFailureNotFound()
    {
        _pagamentoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Pagamento?)null);

        var result = await _handler.HandleAsync(new ObterStatusPagamentoQuery(Guid.NewGuid(), Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("pagamento_nao_encontrado");
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task HandleAsync_QueryNula_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
