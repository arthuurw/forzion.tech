using FluentAssertions;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Admin.Alunos.ListarAlunosAdmin;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Tests.Builders;
using Moq;

namespace forzion.tech.Tests.Application.Admin.Alunos;

public class ListarAlunosAdminHandlerTests
{
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly ListarAlunosAdminHandler _handler;

    public ListarAlunosAdminHandlerTests()
    {
        _handler = new ListarAlunosAdminHandler(_alunoRepo.Object);
    }

    [Fact]
    public async Task HandleAsync_QueryNula_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task HandleAsync_SemAlunos_RetornaListaVaziaComPaginacao()
    {
        _alunoRepo
            .Setup(r => r.ListarTodosAsync(1, 20, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Aluno>(), 0));

        var result = await _handler.HandleAsync(new ListarAlunosAdminQuery());

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
        result.Pagina.Should().Be(1);
        result.TamanhoPagina.Should().Be(20);
    }

    [Fact]
    public async Task HandleAsync_ComAlunos_MapeiaResponseEReflectePaginacao()
    {
        var aluno = new AlunoBuilder().ComNome("João Silva").ComEmail("joao@teste.com").Build();
        _alunoRepo
            .Setup(r => r.ListarTodosAsync(2, 5, "João", AlunoStatus.AguardandoAprovacao, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Aluno> { aluno }, 1));

        var query = new ListarAlunosAdminQuery(2, 5, "João", AlunoStatus.AguardandoAprovacao);
        var result = await _handler.HandleAsync(query);

        result.Items.Should().ContainSingle();
        result.Items[0].AlunoId.Should().Be(aluno.Id);
        result.Items[0].Nome.Should().Be("João Silva");
        result.Items[0].Email.Should().Be("joao@teste.com");
        result.Items[0].Status.Should().Be(AlunoStatus.AguardandoAprovacao);
        result.Total.Should().Be(1);
        result.Pagina.Should().Be(2);
        result.TamanhoPagina.Should().Be(5);
    }

    [Fact]
    public async Task HandleAsync_RepassaFiltrosNomeEStatusAoRepositorio()
    {
        _alunoRepo
            .Setup(r => r.ListarTodosAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<AlunoStatus?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Aluno>(), 0));

        await _handler.HandleAsync(new ListarAlunosAdminQuery(3, 10, "Maria", AlunoStatus.Ativo));

        _alunoRepo.Verify(r => r.ListarTodosAsync(3, 10, "Maria", AlunoStatus.Ativo, It.IsAny<CancellationToken>()), Times.Once);
    }
}
