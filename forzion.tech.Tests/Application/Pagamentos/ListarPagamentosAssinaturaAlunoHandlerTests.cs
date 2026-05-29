using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Pagamentos.ListarPagamentosAssinaturaAluno;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Moq;

namespace forzion.tech.Tests.Application.Pagamentos;

public class ListarPagamentosAssinaturaAlunoHandlerTests
{
    private readonly Mock<IPagamentoRepository> _pagamentoRepo = new();
    private readonly Mock<IAssinaturaAlunoRepository> _assinaturaRepo = new();
    private readonly ListarPagamentosAssinaturaAlunoHandler _handler;

    public ListarPagamentosAssinaturaAlunoHandlerTests()
    {
        _handler = new ListarPagamentosAssinaturaAlunoHandler(_pagamentoRepo.Object, _assinaturaRepo.Object);
    }

    [Fact]
    public async Task HandleAsync_DonoDaAssinaturaAluno_RetornaLista()
    {
        var alunoId = Guid.NewGuid();
        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), alunoId, 100m, DateTime.UtcNow).Value;
        var pagamentos = new List<Pagamento>
        {
            Pagamento.Criar(assinatura.Id, 100m, DateTime.UtcNow).Value,
            Pagamento.Criar(assinatura.Id, 200m, DateTime.UtcNow).Value,
        };
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);
        _pagamentoRepo.Setup(r => r.ListarPorAssinaturaAlunoAsync(assinatura.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagamentos);

        var result = await _handler.HandleAsync(new ListarPagamentosAssinaturaAlunoQuery(assinatura.Id, alunoId));

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task HandleAsync_AlunoErrado_LancaAcessoNegado()
    {
        var assinatura = AssinaturaAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100m, DateTime.UtcNow).Value;
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(assinatura.Id, It.IsAny<CancellationToken>())).ReturnsAsync(assinatura);

        var act = async () => await _handler.HandleAsync(new ListarPagamentosAssinaturaAlunoQuery(assinatura.Id, Guid.NewGuid()));
        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_AssinaturaAlunoNaoEncontrada_LancaAcessoNegado()
    {
        _assinaturaRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssinaturaAluno?)null);

        var act = async () => await _handler.HandleAsync(new ListarPagamentosAssinaturaAlunoQuery(Guid.NewGuid(), Guid.NewGuid()));
        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_QueryNula_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
