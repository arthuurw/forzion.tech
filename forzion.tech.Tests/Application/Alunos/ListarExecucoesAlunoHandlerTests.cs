using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.ListarExecucoesAluno;
using forzion.tech.Domain.Exceptions;
using Moq;

namespace forzion.tech.Tests.Application.Alunos;

public class ListarExecucoesAlunoHandlerTests
{
    private readonly Mock<IExecucaoTreinoRepository> _execucaoRepo = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly ListarExecucoesAlunoHandler _handler;

    public ListarExecucoesAlunoHandlerTests()
    {
        _handler = new ListarExecucoesAlunoHandler(_execucaoRepo.Object, _userContext.Object);
    }

    private static ExecucaoComNome CriarExecucaoComNome(Guid alunoId) =>
        new(Guid.NewGuid(), Guid.NewGuid(), alunoId, DateTime.UtcNow, null, DateTime.UtcNow, "Treino A", 3, 9);

    [Fact]
    public async Task HandleAsync_SemExecucoes_RetornaListaVazia()
    {
        var alunoId = Guid.NewGuid();
        _userContext.Setup(u => u.PerfilId).Returns(alunoId);
        _userContext.Setup(u => u.TipoConta).Returns(forzion.tech.Domain.Enums.TipoConta.Aluno);
        _execucaoRepo.Setup(r => r.ListarComNomePorAlunoAsync(alunoId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExecucaoComNome>());
        _execucaoRepo.Setup(r => r.ContarPorAlunoAsync(alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _handler.HandleAsync(alunoId, 1, 10);

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_ComExecucoes_RetornaItemsMapeados()
    {
        var alunoId = Guid.NewGuid();
        _userContext.Setup(u => u.PerfilId).Returns(alunoId);
        _userContext.Setup(u => u.TipoConta).Returns(forzion.tech.Domain.Enums.TipoConta.Aluno);
        var execucoes = new List<ExecucaoComNome> { CriarExecucaoComNome(alunoId), CriarExecucaoComNome(alunoId) };
        _execucaoRepo.Setup(r => r.ListarComNomePorAlunoAsync(alunoId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(execucoes);
        _execucaoRepo.Setup(r => r.ContarPorAlunoAsync(alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var result = await _handler.HandleAsync(alunoId, 1, 10);

        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(2);
        result.Items.Should().AllSatisfy(e => e.AlunoId.Should().Be(alunoId));
    }

    [Fact]
    public async Task HandleAsync_ComExecucoes_MapeiaTodosOsCamposDoResponse()
    {
        var alunoId = Guid.NewGuid();
        var execucaoId = Guid.NewGuid();
        var treinoId = Guid.NewGuid();
        var dataExec = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
        var createdAt = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        var execucao = new ExecucaoComNome(
            execucaoId, treinoId, alunoId, dataExec, "Treino concluído", createdAt, "Treino Peito", 5, 18);

        _userContext.Setup(u => u.PerfilId).Returns(alunoId);
        _userContext.Setup(u => u.TipoConta).Returns(forzion.tech.Domain.Enums.TipoConta.Aluno);
        _execucaoRepo.Setup(r => r.ListarComNomePorAlunoAsync(alunoId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExecucaoComNome> { execucao });
        _execucaoRepo.Setup(r => r.ContarPorAlunoAsync(alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _handler.HandleAsync(alunoId, 1, 10);

        result.Items.Should().ContainSingle();
        var item = result.Items[0];
        item.ExecucaoId.Should().Be(execucaoId);
        item.TreinoId.Should().Be(treinoId);
        item.AlunoId.Should().Be(alunoId);
        item.DataExecucao.Should().Be(dataExec);
        item.Observacao.Should().Be("Treino concluído");
        item.CreatedAt.Should().Be(createdAt);
        item.NomeTreino.Should().Be("Treino Peito");
        item.TotalExercicios.Should().Be(5);
        item.TotalSeries.Should().Be(18);
    }

    [Fact]
    public async Task HandleAsync_SystemAdmin_IgnoraVerificacaoDono()
    {
        var alunoId = Guid.NewGuid();
        _userContext.Setup(u => u.IsSystemAdmin).Returns(true);
        _userContext.Setup(u => u.PerfilId).Returns(Guid.NewGuid());
        _execucaoRepo.Setup(r => r.ListarComNomePorAlunoAsync(alunoId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExecucaoComNome>());
        _execucaoRepo.Setup(r => r.ContarPorAlunoAsync(alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _handler.HandleAsync(alunoId, 1, 10);

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_PaginacaoCorreta_RefleteNaResposta()
    {
        var alunoId = Guid.NewGuid();
        _userContext.Setup(u => u.PerfilId).Returns(alunoId);
        _userContext.Setup(u => u.TipoConta).Returns(forzion.tech.Domain.Enums.TipoConta.Aluno);
        _execucaoRepo.Setup(r => r.ListarComNomePorAlunoAsync(alunoId, 3, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExecucaoComNome>());
        _execucaoRepo.Setup(r => r.ContarPorAlunoAsync(alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _handler.HandleAsync(alunoId, 3, 5);

        result.Pagina.Should().Be(3);
        result.TamanhoPagina.Should().Be(5);
    }

    [Fact]
    public async Task HandleAsync_AlunoIdDiferente_LancaAcessoNegadoException()
    {
        var alunoId = Guid.NewGuid();
        var outroId = Guid.NewGuid();
        _userContext.Setup(u => u.PerfilId).Returns(alunoId);
        _userContext.Setup(u => u.TipoConta).Returns(forzion.tech.Domain.Enums.TipoConta.Aluno);

        var act = async () => await _handler.HandleAsync(outroId, 1, 10);

        await act.Should().ThrowAsync<AcessoNegadoException>();
    }
}
