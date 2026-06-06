using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Vinculos.AprovarVinculo;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Application.Vinculos;

public class AprovarVinculoHandlerTests
{
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly Mock<ITreinoAlunoRepository> _treinoAlunoRepo = new();
    private readonly Mock<ITreinoRepository> _treinoRepo = new();
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IContaRecebimentoRepository> _contaRecebimentoRepo = new();
    private readonly Mock<ILimiteTreinadorService> _limiteService = new();
    private readonly Mock<ILogAprovacaoRepository> _logRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IDbContextTransactionProvider> _transactionProvider = new();
    private readonly Mock<ILogger<AprovarVinculoHandler>> _logger = new();
    private readonly AprovarVinculoHandler _handler;

    public AprovarVinculoHandlerTests()
    {
        _alunoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Aluno?)null);
        var mockTx = new Mock<ITransaction>();
        mockTx.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        mockTx.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);
        _transactionProvider
            .Setup(p => p.BeginTransactionAsync(It.IsAny<System.Data.IsolationLevel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockTx.Object);
        var contaOnboarded = ContaRecebimento.Criar(Guid.NewGuid(), DateTime.UtcNow).Value;
        contaOnboarded.ConfigurarStripeConnect("acct_123", DateTime.UtcNow);
        contaOnboarded.ConfirmarOnboarding(DateTime.UtcNow);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(contaOnboarded);
        var treinadorPlataforma = Treinador.Criar(Guid.NewGuid(), "Treinador", DateTime.UtcNow, modoPagamentoAluno: ModoPagamentoAluno.Plataforma).Value;
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(treinadorPlataforma);
        _handler = new AprovarVinculoHandler(
            _vinculoRepo.Object,
            _treinoAlunoRepo.Object,
            _treinoRepo.Object,
            _alunoRepo.Object,
            _treinadorRepo.Object,
            _contaRecebimentoRepo.Object,
            _limiteService.Object,
            _logRepo.Object,
            _unitOfWork.Object,
            _transactionProvider.Object,
            TimeProvider.System,
            _logger.Object);
    }

    [Fact]
    public async Task HandleAsync_TreinadorSemOnboarding_FalhaSemAprovar()
    {
        var treinadorId = Guid.NewGuid();
        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, Guid.NewGuid(), DateTime.UtcNow).Value;
        _vinculoRepo.Setup(r => r.ObterPorIdAsync(vinculo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContaRecebimento?)null);

        var result = await _handler.HandleAsync(new AprovarVinculoCommand(vinculo.Id, treinadorId, Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("treinador_sem_onboarding");
        _logRepo.Verify(r => r.AdicionarAsync(It.IsAny<LogAprovacao>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ModoExterno_SemOnboarding_Aprova()
    {
        var treinadorId = Guid.NewGuid();
        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, Guid.NewGuid(), DateTime.UtcNow).Value;
        var treinadorExterno = Treinador.Criar(Guid.NewGuid(), "Externo", DateTime.UtcNow, modoPagamentoAluno: ModoPagamentoAluno.Externo).Value;
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(treinadorExterno);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinadorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContaRecebimento?)null);
        _vinculoRepo.Setup(r => r.ObterPorIdAsync(vinculo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);
        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(vinculo.AlunoId, It.IsAny<CancellationToken>())).ReturnsAsync((VinculoTreinadorAluno?)null);
        _limiteService.Setup(s => s.ValidarAsync(treinadorId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(new AprovarVinculoCommand(vinculo.Id, treinadorId, Guid.NewGuid()));

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(VinculoStatus.Ativo);
        _logRepo.Verify(r => r.AdicionarAsync(It.IsAny<LogAprovacao>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_LancaException()
    {
        var treinadorId = Guid.NewGuid();
        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, Guid.NewGuid(), DateTime.UtcNow).Value;
        _vinculoRepo.Setup(r => r.ObterPorIdAsync(vinculo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync((Treinador?)null);

        var act = async () => await _handler.HandleAsync(new AprovarVinculoCommand(vinculo.Id, treinadorId, Guid.NewGuid()));
        await act.Should().ThrowAsync<TreinadorNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_VinculoValido_Aprova()
    {
        var treinadorId = Guid.NewGuid();
        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, Guid.NewGuid(), DateTime.UtcNow).Value;
        var pacoteId = Guid.NewGuid();

        _vinculoRepo.Setup(r => r.ObterPorIdAsync(vinculo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);
        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(vinculo.AlunoId, It.IsAny<CancellationToken>())).ReturnsAsync((VinculoTreinadorAluno?)null);
        _limiteService.Setup(s => s.ValidarAsync(treinadorId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(new AprovarVinculoCommand(vinculo.Id, treinadorId, pacoteId));

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(VinculoStatus.Ativo);
        result.Value.PacoteId.Should().Be(pacoteId);
        _logRepo.Verify(r => r.AdicionarAsync(It.IsAny<LogAprovacao>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AlunoJaVinculado_LancaException()
    {
        var treinadorId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();
        var vinculoPendente = VinculoTreinadorAluno.Criar(treinadorId, alunoId, DateTime.UtcNow).Value;
        var vinculoAtivo = VinculoTreinadorAluno.Criar(treinadorId, alunoId, DateTime.UtcNow).Value;

        _vinculoRepo.Setup(r => r.ObterPorIdAsync(vinculoPendente.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculoPendente);
        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync(vinculoAtivo);

        var act = async () => await _handler.HandleAsync(new AprovarVinculoCommand(vinculoPendente.Id, treinadorId, Guid.NewGuid()));
        await act.Should().ThrowAsync<AlunoJaVinculadoException>();
    }

    [Fact]
    public async Task HandleAsync_LimiteAtingido_LancaException()
    {
        var treinadorId = Guid.NewGuid();
        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, Guid.NewGuid(), DateTime.UtcNow).Value;

        _vinculoRepo.Setup(r => r.ObterPorIdAsync(vinculo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);
        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(vinculo.AlunoId, It.IsAny<CancellationToken>())).ReturnsAsync((VinculoTreinadorAluno?)null);
        _limiteService.Setup(s => s.ValidarAsync(treinadorId, It.IsAny<CancellationToken>())).ThrowsAsync(new LimiteAlunosAtingidoException());

        var act = async () => await _handler.HandleAsync(new AprovarVinculoCommand(vinculo.Id, treinadorId, Guid.NewGuid()));
        await act.Should().ThrowAsync<LimiteAlunosAtingidoException>();
    }

    [Fact]
    public async Task HandleAsync_TreinadorDiferente_LancaAcessoNegado()
    {
        var vinculo = VinculoTreinadorAluno.Criar(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow).Value;
        _vinculoRepo.Setup(r => r.ObterPorIdAsync(vinculo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);

        var act = async () => await _handler.HandleAsync(new AprovarVinculoCommand(vinculo.Id, Guid.NewGuid(), Guid.NewGuid()));
        await act.Should().ThrowAsync<AcessoNegadoException>();
    }

    [Fact]
    public async Task HandleAsync_VinculoNaoEncontrado_LancaVinculoNaoEncontradoException()
    {
        var vinculoId = Guid.NewGuid();
        _vinculoRepo.Setup(r => r.ObterPorIdAsync(vinculoId, It.IsAny<CancellationToken>())).ReturnsAsync((VinculoTreinadorAluno?)null);

        var act = async () => await _handler.HandleAsync(new AprovarVinculoCommand(vinculoId, Guid.NewGuid(), Guid.NewGuid()));
        await act.Should().ThrowAsync<VinculoNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_VinculoJaAtivo_RetornaFalha()
    {
        var treinadorId = Guid.NewGuid();
        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, Guid.NewGuid(), DateTime.UtcNow).Value;
        vinculo.Aprovar(treinadorId, Guid.NewGuid(), TestData.Agora);

        _vinculoRepo.Setup(r => r.ObterPorIdAsync(vinculo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);
        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(vinculo.AlunoId, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);
        _limiteService.Setup(s => s.ValidarAsync(treinadorId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(new AprovarVinculoCommand(vinculo.Id, treinadorId, Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("aguardando aprovação");
    }

    [Fact]
    public async Task HandleAsync_VinculoInativo_RetornaFalha()
    {
        var treinadorId = Guid.NewGuid();
        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, Guid.NewGuid(), DateTime.UtcNow).Value;
        vinculo.Inativar(TestData.Agora);

        _vinculoRepo.Setup(r => r.ObterPorIdAsync(vinculo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);
        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(vinculo.AlunoId, It.IsAny<CancellationToken>())).ReturnsAsync((VinculoTreinadorAluno?)null);
        _limiteService.Setup(s => s.ValidarAsync(treinadorId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(new AprovarVinculoCommand(vinculo.Id, treinadorId, Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("aguardando aprovação");
    }

    [Fact]
    public async Task HandleAsync_VinculoValido_CommitaUmaVez()
    {
        var treinadorId = Guid.NewGuid();
        var vinculo = VinculoTreinadorAluno.Criar(treinadorId, Guid.NewGuid(), DateTime.UtcNow).Value;
        var pacoteId = Guid.NewGuid();

        _vinculoRepo.Setup(r => r.ObterPorIdAsync(vinculo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculo);
        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(vinculo.AlunoId, It.IsAny<CancellationToken>())).ReturnsAsync((VinculoTreinadorAluno?)null);
        _limiteService.Setup(s => s.ValidarAsync(treinadorId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await _handler.HandleAsync(new AprovarVinculoCommand(vinculo.Id, treinadorId, pacoteId));

        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task HandleAsync_TrocaDeTreinador_InativaVinculoAnteriorEFichasAntigas()
    {
        var treinadorAntigoId = Guid.NewGuid();
        var treinadorNovoId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();

        var vinculoPendente = VinculoTreinadorAluno.Criar(treinadorNovoId, alunoId, TestData.Agora).Value;
        var vinculoAnterior = VinculoTreinadorAluno.Criar(treinadorAntigoId, alunoId, TestData.Agora).Value;
        vinculoAnterior.Aprovar(treinadorAntigoId, Guid.NewGuid(), TestData.Agora);

        var fichaAntiga = TreinoAluno.Criar(Guid.NewGuid(), alunoId, TestData.Agora).Value;

        var treinadorNovo = Treinador.Criar(treinadorNovoId, "Novo", TestData.Agora, modoPagamentoAluno: ModoPagamentoAluno.Plataforma).Value;
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinadorNovoId, It.IsAny<CancellationToken>())).ReturnsAsync(treinadorNovo);

        var contaOnboarded = ContaRecebimento.Criar(treinadorNovoId, TestData.Agora).Value;
        contaOnboarded.ConfigurarStripeConnect("acct_novo", TestData.Agora);
        contaOnboarded.ConfirmarOnboarding(TestData.Agora);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinadorNovoId, It.IsAny<CancellationToken>())).ReturnsAsync(contaOnboarded);

        _vinculoRepo.Setup(r => r.ObterPorIdAsync(vinculoPendente.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculoPendente);
        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync(vinculoAnterior);
        _treinoAlunoRepo.Setup(r => r.ListarAtivosPorParAsync(treinadorAntigoId, alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TreinoAluno> { fichaAntiga }.AsReadOnly() as IReadOnlyList<TreinoAluno>);
        _limiteService.Setup(s => s.ValidarAsync(treinadorNovoId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(new AprovarVinculoCommand(vinculoPendente.Id, treinadorNovoId, Guid.NewGuid()));

        result.IsSuccess.Should().BeTrue();
        vinculoAnterior.Status.Should().Be(VinculoStatus.Inativo, "vínculo anterior deve ser inativado na troca");
        fichaAntiga.Status.Should().Be(TreinoAlunoStatus.Inativo, "fichas do treinador anterior são inativadas");
    }

    [Fact]
    public async Task HandleAsync_TrocaDeTreinador_ComTrarFichas_DuplicaFichasParaNovoTreinador()
    {
        var treinadorAntigoId = Guid.NewGuid();
        var treinadorNovoId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();

        var vinculoPendente = VinculoTreinadorAluno.Criar(treinadorNovoId, alunoId, TestData.Agora).Value;
        var vinculoAnterior = VinculoTreinadorAluno.Criar(treinadorAntigoId, alunoId, TestData.Agora).Value;
        vinculoAnterior.Aprovar(treinadorAntigoId, Guid.NewGuid(), TestData.Agora);

        var treinoOrigem = Treino.Criar("Treino A", ObjetivoTreino.Hipertrofia, treinadorAntigoId, TestData.Agora).Value;
        var fichaAntiga = TreinoAluno.Criar(treinoOrigem.Id, alunoId, TestData.Agora).Value;

        var treinadorNovo = Treinador.Criar(treinadorNovoId, "Novo", TestData.Agora, modoPagamentoAluno: ModoPagamentoAluno.Plataforma).Value;
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinadorNovoId, It.IsAny<CancellationToken>())).ReturnsAsync(treinadorNovo);

        var contaOnboarded = ContaRecebimento.Criar(treinadorNovoId, TestData.Agora).Value;
        contaOnboarded.ConfigurarStripeConnect("acct_novo2", TestData.Agora);
        contaOnboarded.ConfirmarOnboarding(TestData.Agora);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinadorNovoId, It.IsAny<CancellationToken>())).ReturnsAsync(contaOnboarded);

        _vinculoRepo.Setup(r => r.ObterPorIdAsync(vinculoPendente.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculoPendente);
        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync(vinculoAnterior);
        _treinoAlunoRepo.Setup(r => r.ListarAtivosPorParAsync(treinadorAntigoId, alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TreinoAluno> { fichaAntiga }.AsReadOnly() as IReadOnlyList<TreinoAluno>);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(treinoOrigem.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinoOrigem);
        _limiteService.Setup(s => s.ValidarAsync(treinadorNovoId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(new AprovarVinculoCommand(vinculoPendente.Id, treinadorNovoId, Guid.NewGuid(), TrarFichas: true));

        result.IsSuccess.Should().BeTrue();
        _treinoRepo.Verify(r => r.ObterPorIdAsync(treinoOrigem.Id, It.IsAny<CancellationToken>()), Times.Once);
        _treinoRepo.Verify(r => r.AdicionarAsync(It.IsAny<Treino>(), It.IsAny<CancellationToken>()), Times.Once);
        _treinoAlunoRepo.Verify(r => r.AdicionarAsync(It.IsAny<TreinoAluno>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TrocaDeTreinador_TrarFichas_TreinoOrigemNulo_IgnoraEProssegue()
    {
        var treinadorAntigoId = Guid.NewGuid();
        var treinadorNovoId = Guid.NewGuid();
        var alunoId = Guid.NewGuid();

        var vinculoPendente = VinculoTreinadorAluno.Criar(treinadorNovoId, alunoId, TestData.Agora).Value;
        var vinculoAnterior = VinculoTreinadorAluno.Criar(treinadorAntigoId, alunoId, TestData.Agora).Value;
        vinculoAnterior.Aprovar(treinadorAntigoId, Guid.NewGuid(), TestData.Agora);

        var fichaOrfã = TreinoAluno.Criar(Guid.NewGuid(), alunoId, TestData.Agora).Value;

        var treinadorNovo = Treinador.Criar(treinadorNovoId, "Novo", TestData.Agora, modoPagamentoAluno: ModoPagamentoAluno.Plataforma).Value;
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinadorNovoId, It.IsAny<CancellationToken>())).ReturnsAsync(treinadorNovo);

        var contaOnboarded = ContaRecebimento.Criar(treinadorNovoId, TestData.Agora).Value;
        contaOnboarded.ConfigurarStripeConnect("acct_novo3", TestData.Agora);
        contaOnboarded.ConfirmarOnboarding(TestData.Agora);
        _contaRecebimentoRepo.Setup(r => r.ObterPorTreinadorIdAsync(treinadorNovoId, It.IsAny<CancellationToken>())).ReturnsAsync(contaOnboarded);

        _vinculoRepo.Setup(r => r.ObterPorIdAsync(vinculoPendente.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vinculoPendente);
        _vinculoRepo.Setup(r => r.ObterAtivoPorAlunoAsync(alunoId, It.IsAny<CancellationToken>())).ReturnsAsync(vinculoAnterior);
        _treinoAlunoRepo.Setup(r => r.ListarAtivosPorParAsync(treinadorAntigoId, alunoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TreinoAluno> { fichaOrfã }.AsReadOnly() as IReadOnlyList<TreinoAluno>);
        _treinoRepo.Setup(r => r.ObterPorIdAsync(fichaOrfã.TreinoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Treino?)null);
        _limiteService.Setup(s => s.ValidarAsync(treinadorNovoId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(new AprovarVinculoCommand(vinculoPendente.Id, treinadorNovoId, Guid.NewGuid(), TrarFichas: true));

        result.IsSuccess.Should().BeTrue("treino origem nulo é ignorado e o fluxo continua");
        _treinoRepo.Verify(r => r.AdicionarAsync(It.IsAny<Treino>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
