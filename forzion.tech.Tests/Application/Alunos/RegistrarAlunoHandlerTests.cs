using FluentAssertions;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.RegistrarAluno;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Alunos;

public class RegistrarAlunoHandlerTests
{
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IVinculoTreinadorAlunoRepository> _vinculoRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IPacoteRepository> _pacoteRepo = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogAprovacaoRepository> _logAprovacaoRepo = new();
    private readonly Mock<ILogger<RegistrarAlunoHandler>> _logger = new();
    private readonly RegistrarAlunoHandler _handler;

    public RegistrarAlunoHandlerTests()
    {
        _passwordHasher.Setup(p => p.Hash(It.IsAny<string>())).Returns("hash");
        _handler = new RegistrarAlunoHandler(
            _contaRepo.Object,
            _alunoRepo.Object,
            _vinculoRepo.Object,
            _treinadorRepo.Object,
            _pacoteRepo.Object,
            _passwordHasher.Object,
            _unitOfWork.Object,
            _logAprovacaoRepo.Object,
            new RegistrarAlunoCommandValidator(),
            TimeProvider.System,
            _logger.Object);
    }

    private (Guid TreinadorId, Pacote Pacote) ArrangeTreinadorAtivoComPacote()
    {
        var treinadorId = Guid.NewGuid();
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        treinador.Aprovar(Guid.NewGuid(), DateTime.UtcNow);
        var pacote = Pacote.Criar(treinadorId, "Basic", 10, DateTime.UtcNow).Value;

        _contaRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Conta?)null);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacote.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pacote);

        return (treinadorId, pacote);
    }

    [Fact]
    public async Task HandleAsync_DadosValidos_CriaContaAlunoEVinculo()
    {
        var treinadorId = Guid.NewGuid();
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        treinador.Aprovar(Guid.NewGuid(), DateTime.UtcNow);
        var pacote = Pacote.Criar(treinadorId, "Basic", 10, DateTime.UtcNow).Value;

        _contaRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Conta?)null);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacote.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pacote);

        var result = await _handler.HandleAsync(new RegistrarAlunoCommand("joao@teste.com", "Senha123", "Joao", treinadorId, pacote.Id));

        result.Value.Nome.Should().Be("Joao");
        result.Value.Status.Should().Be(AlunoStatus.AguardandoAprovacao);
        _contaRepo.Verify(r => r.AdicionarAsync(It.IsAny<Conta>(), It.IsAny<CancellationToken>()), Times.Once);
        _alunoRepo.Verify(r => r.AdicionarAsync(It.IsAny<Aluno>(), It.IsAny<CancellationToken>()), Times.Once);
        _vinculoRepo.Verify(r => r.AdicionarAsync(It.IsAny<VinculoTreinadorAluno>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TreinadorInativo_LancaDomainException()
    {
        var treinadorId = Guid.NewGuid();
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        treinador.Aprovar(Guid.NewGuid(), DateTime.UtcNow);
        treinador.Inativar(DateTime.UtcNow);

        _contaRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Conta?)null);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var act = async () => await _handler.HandleAsync(new RegistrarAlunoCommand("joao@teste.com", "Senha123", "Joao", treinadorId, Guid.NewGuid()));
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*não disponível*");
    }

    [Fact]
    public async Task HandleAsync_TreinadorAguardandoAprovacao_LancaDomainException()
    {
        var treinadorId = Guid.NewGuid();
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        // Status padrão = AguardandoAprovacao

        _contaRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Conta?)null);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);

        var act = async () => await _handler.HandleAsync(new RegistrarAlunoCommand("joao@teste.com", "Senha123", "Joao", treinadorId, Guid.NewGuid()));
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*não disponível*");
    }

    [Fact]
    public async Task HandleAsync_EmailJaCadastrado_LancaException()
    {
        var conta = global::forzion.tech.Domain.Entities.Conta.Criar(Email.Criar("joao@teste.com").Value, "hash", TipoConta.Aluno, DateTime.UtcNow).Value;
        _contaRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(conta);

        var act = async () => await _handler.HandleAsync(new RegistrarAlunoCommand("joao@teste.com", "Senha123", "Joao", Guid.NewGuid(), Guid.NewGuid()));
        await act.Should().ThrowAsync<EmailJaCadastradoException>();
    }

    [Fact]
    public async Task HandleAsync_TreinadorNaoEncontrado_LancaException()
    {
        _contaRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Conta?)null);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Treinador?)null);

        var act = async () => await _handler.HandleAsync(new RegistrarAlunoCommand("joao@teste.com", "Senha123", "Joao", Guid.NewGuid(), Guid.NewGuid()));
        await act.Should().ThrowAsync<TreinadorNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_DadosInvalidos_LancaValidationException()
    {
        var act = async () => await _handler.HandleAsync(new RegistrarAlunoCommand("invalido", "123", "", Guid.Empty, Guid.Empty));
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_PacoteNaoEncontrado_LancaPacoteNaoEncontradoException()
    {
        var treinadorId = Guid.NewGuid();
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        treinador.Aprovar(Guid.NewGuid(), DateTime.UtcNow);

        _contaRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Conta?)null);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Pacote?)null);

        var act = async () => await _handler.HandleAsync(
            new RegistrarAlunoCommand("joao@teste.com", "Senha123", "Joao", treinadorId, Guid.NewGuid()));

        await act.Should().ThrowAsync<PacoteNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_PacoteDeOutroTreinador_RetornaFailure()
    {
        var treinadorId = Guid.NewGuid();
        var treinador = Treinador.Criar(Guid.NewGuid(), "Carlos", DateTime.UtcNow).Value;
        treinador.Aprovar(Guid.NewGuid(), DateTime.UtcNow);
        // Pacote pertence a OUTRO treinador.
        var pacote = Pacote.Criar(Guid.NewGuid(), "Basic", 10, DateTime.UtcNow).Value;

        _contaRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Conta?)null);
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinadorId, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _pacoteRepo.Setup(r => r.ObterPorIdAsync(pacote.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pacote);

        var result = await _handler.HandleAsync(
            new RegistrarAlunoCommand("joao@teste.com", "Senha123", "Joao", treinadorId, pacote.Id));

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("não pertence ao treinador");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AnamneseSemConsentimento_LancaValidationException()
    {
        var (treinadorId, pacote) = ArrangeTreinadorAtivoComPacote();

        var act = async () => await _handler.HandleAsync(new RegistrarAlunoCommand(
            "joao@teste.com", "Senha123", "Joao", treinadorId, pacote.Id,
            Doencas: "Hipertensão",
            ConsentimentoDadosSaude: false));

        await act.Should().ThrowAsync<ValidationException>()
            .Where(e => e.Errors.Any(f => f.ErrorCode == "consentimento_saude_obrigatorio"));
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AnamneseComConsentimento_RegistraLogAprovacaoComTimestampDoCliente()
    {
        var (treinadorId, pacote) = ArrangeTreinadorAtivoComPacote();
        var consentidoEm = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        LogAprovacao? logRegistrado = null;
        _logAprovacaoRepo
            .Setup(r => r.AdicionarAsync(It.IsAny<LogAprovacao>(), It.IsAny<CancellationToken>()))
            .Callback<LogAprovacao, CancellationToken>((l, _) => logRegistrado = l);

        var result = await _handler.HandleAsync(new RegistrarAlunoCommand(
            "joao@teste.com", "Senha123", "Joao", treinadorId, pacote.Id,
            Doencas: "Hipertensão",
            ConsentimentoDadosSaude: true,
            ConsentimentoDadosSaudeEm: consentidoEm));

        result.IsSuccess.Should().BeTrue();
        logRegistrado.Should().NotBeNull();
        logRegistrado!.TipoAcao.Should().Be(TipoAcaoAprovacao.ConsentimentoAnamnese);
        logRegistrado.CreatedAt.Should().Be(consentidoEm);
        logRegistrado.EntidadeTipo.Should().Be("Conta");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SemDadosSensiveis_NaoExigeConsentimentoNemRegistraLog()
    {
        var (treinadorId, pacote) = ArrangeTreinadorAtivoComPacote();

        var result = await _handler.HandleAsync(new RegistrarAlunoCommand(
            "joao@teste.com", "Senha123", "Joao", treinadorId, pacote.Id));

        result.IsSuccess.Should().BeTrue();
        _logAprovacaoRepo.Verify(r => r.AdicionarAsync(It.IsAny<LogAprovacao>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
