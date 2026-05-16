using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Pagamentos.ListarPagamentosAssinatura;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Moq;

namespace forzion.tech.Tests.Application.Pagamentos;

public class ListarPagamentosAssinaturaHandlerTests
{
    private readonly Mock<IPagamentoRepository> _pagamentoRepo = new();
    private readonly Mock<IAssinaturaRepository> _assinaturaRepo = new();
    private readonly ListarPagamentosAssinaturaHandler _handler;

    public ListarPagamentosAssinaturaHandlerTests()
    {
        _handler = new ListarPagamentosAssinaturaHandler(_pagamentoRepo.Object, _assinaturaRepo.Object);
    }

    [Fact]
    public async Task HandleAsync_DonoDaAssinatura_RetornaLista()
    {
        var alunoId = Guid.NewGuid();
        var assinatura = Assinatura.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), alunoId, 100m);
        var pagamentos = new List<Pagamento>
        {
            Pagamento.Criar(assinatura.Id, 100m),
            Pagamento.Criar(assinatura.Id, 200m),
        };
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ListarPorAssinaturaAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamentos);

        var result = await _handler.HandleAsync(new ListarPagamentosAssinaturaQuery(assinatura.Id, alunoId));

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task HandleAsync_AlunoErrado_LancaAcessoNegado()
    {
        var assinatura = Assinatura.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m);
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        var act = async () => await _handler.HandleAsync(new ListarPagamentosAssinaturaQuery(assinatura.Id, Guid.NewGuid()));
        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_AssinaturaNaoEncontrada_LancaAcessoNegado()
    {
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Assinatura?)null);

        var act = async () => await _handler.HandleAsync(new ListarPagamentosAssinaturaQuery(Guid.NewGuid(), Guid.NewGuid()));
        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_QueryNula_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
