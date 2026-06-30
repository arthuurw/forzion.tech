using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos;
using forzion.tech.Application.UseCases.Alunos.AtualizarAnamneseAluno;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Tests.Builders;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Alunos;

public class AtualizarAnamneseAlunoHandlerTests
{
    private readonly Mock<IAlunoRepository> _alunoRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<ILogAprovacaoRepository> _logAprovacaoRepo = new();
    private readonly Mock<ILogger<AtualizarAnamneseAlunoHandler>> _logger = new();
    private readonly AtualizarAnamneseAlunoHandler _handler;

    public AtualizarAnamneseAlunoHandlerTests()
    {
        _userContext.Setup(c => c.IsAluno).Returns(true);
        _handler = new AtualizarAnamneseAlunoHandler(
            _alunoRepo.Object,
            _unitOfWork.Object,
            _userContext.Object,
            _logAprovacaoRepo.Object,
            new AtualizarAnamneseAlunoCommandValidator(),
            TimeProvider.System,
            _logger.Object);
    }

    private Aluno ArrangeAlunoProprio(Guid? contaId = null)
    {
        var aluno = new AlunoBuilder().ComContaId(contaId ?? TestData.NextGuid()).ComNome("João").Build();
        _userContext.Setup(c => c.PerfilId).Returns(aluno.Id);
        _alunoRepo.Setup(r => r.ObterPorIdAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);
        return aluno;
    }

    [Fact]
    public async Task HandleAsync_CampoNaoSensivel_PersisteCorrecao()
    {
        var aluno = ArrangeAlunoProprio();

        var result = await _handler.HandleAsync(new AtualizarAnamneseAlunoCommand(aluno.Id, DiasDisponiveis: 4));

        result.IsSuccess.Should().BeTrue();
        result.Value.DiasDisponiveis.Should().Be(4);
        aluno.DiasDisponiveis.Should().Be(4);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _logAprovacaoRepo.Verify(r => r.AdicionarAsync(It.IsAny<LogAprovacao>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SemAnamnesePrevia_PreencheSemErro()
    {
        var aluno = ArrangeAlunoProprio();
        aluno.Finalidade.Should().BeNull();

        var result = await _handler.HandleAsync(new AtualizarAnamneseAlunoCommand(
            aluno.Id,
            Finalidade: FinalidadeTreino.Hipertrofia,
            ConsentimentoDadosSaude: true));

        result.IsSuccess.Should().BeTrue();
        aluno.Finalidade.Should().Be(FinalidadeTreino.Hipertrofia);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_DadoSaudeSemConsentimento_LancaValidationException()
    {
        var aluno = ArrangeAlunoProprio();

        var act = async () => await _handler.HandleAsync(new AtualizarAnamneseAlunoCommand(
            aluno.Id, Doencas: "Hipertensão", ConsentimentoDadosSaude: false));

        await act.Should().ThrowAsync<ValidationException>()
            .Where(e => e.Errors.Any(f => f.ErrorCode == "consentimento_saude_obrigatorio"));
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_DadoSaudeComConsentimento_RegistraLogConsentimentoAnamnese()
    {
        var contaId = TestData.NextGuid();
        var aluno = ArrangeAlunoProprio(contaId);
        var antesDaChamada = DateTime.UtcNow;
        var consentidoEmCliente = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        LogAprovacao? logRegistrado = null;
        _logAprovacaoRepo
            .Setup(r => r.AdicionarAsync(It.IsAny<LogAprovacao>(), It.IsAny<CancellationToken>()))
            .Callback<LogAprovacao, CancellationToken>((l, _) => logRegistrado = l);

        var result = await _handler.HandleAsync(new AtualizarAnamneseAlunoCommand(
            aluno.Id,
            Doencas: "Hipertensão",
            ConsentimentoDadosSaude: true,
            ConsentimentoDadosSaudeEm: consentidoEmCliente));

        result.IsSuccess.Should().BeTrue();
        aluno.Doencas.Should().Be("Hipertensão");
        logRegistrado.Should().NotBeNull();
        logRegistrado!.TipoAcao.Should().Be(TipoAcaoAprovacao.ConsentimentoAnamnese);
        logRegistrado.EntidadeTipo.Should().Be("Conta");
        logRegistrado.RealizadoPorId.Should().Be(contaId);
        logRegistrado.EntidadeId.Should().Be(contaId);
        logRegistrado.Observacao.Should().Contain("cliente reportou");
        logRegistrado.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
        logRegistrado.CreatedAt.Should().NotBe(consentidoEmCliente);
        logRegistrado.CreatedAt.Should().BeOnOrAfter(antesDaChamada).And.BeOnOrBefore(DateTime.UtcNow);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_DadoSaudeSemReportado_ObservacaoUsaVersaoDaConstante()
    {
        var aluno = ArrangeAlunoProprio();
        LogAprovacao? logRegistrado = null;
        _logAprovacaoRepo
            .Setup(r => r.AdicionarAsync(It.IsAny<LogAprovacao>(), It.IsAny<CancellationToken>()))
            .Callback<LogAprovacao, CancellationToken>((l, _) => logRegistrado = l);

        var result = await _handler.HandleAsync(new AtualizarAnamneseAlunoCommand(
            aluno.Id,
            Doencas: "Hipertensão",
            ConsentimentoDadosSaude: true));

        result.IsSuccess.Should().BeTrue();
        logRegistrado.Should().NotBeNull();
        logRegistrado!.Observacao.Should().Be(AnamneseConsentimento.Versao);
    }

    [Fact]
    public async Task HandleAsync_OutroAluno_LancaAcessoNegadoException()
    {
        var aluno = new AlunoBuilder().ComNome("João").Build();
        _userContext.Setup(c => c.PerfilId).Returns(Guid.NewGuid());
        _alunoRepo.Setup(r => r.ObterPorIdAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);

        var act = async () => await _handler.HandleAsync(new AtualizarAnamneseAlunoCommand(aluno.Id, DiasDisponiveis: 3));

        await act.Should().ThrowAsync<AcessoNegadoException>();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_RoleNaoAluno_LancaAcessoNegadoException()
    {
        var aluno = new AlunoBuilder().ComNome("João").Build();
        _userContext.Setup(c => c.IsAluno).Returns(false);
        _userContext.Setup(c => c.PerfilId).Returns(aluno.Id);
        _alunoRepo.Setup(r => r.ObterPorIdAsync(aluno.Id, It.IsAny<CancellationToken>())).ReturnsAsync(aluno);

        var act = async () => await _handler.HandleAsync(new AtualizarAnamneseAlunoCommand(aluno.Id, DiasDisponiveis: 3));

        await act.Should().ThrowAsync<AcessoNegadoException>();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AlunoNaoEncontrado_LancaAlunoNaoEncontradoException()
    {
        _userContext.Setup(c => c.PerfilId).Returns(Guid.NewGuid());
        _alunoRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Aluno?)null);

        var act = async () => await _handler.HandleAsync(new AtualizarAnamneseAlunoCommand(Guid.NewGuid(), DiasDisponiveis: 3));

        await act.Should().ThrowAsync<AlunoNaoEncontradoException>();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CommandNulo_LancaArgumentNullException()
    {
        var act = async () => await _handler.HandleAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task HandleAsync_DadoSaudeSemConsentimento_ValidatorContornado_NaoPersiste()
    {
        var aluno = ArrangeAlunoProprio();
        var validatorNoOp = new Mock<IValidator<AtualizarAnamneseAlunoCommand>>();
        validatorNoOp
            .Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        var handler = new AtualizarAnamneseAlunoHandler(
            _alunoRepo.Object, _unitOfWork.Object, _userContext.Object, _logAprovacaoRepo.Object,
            validatorNoOp.Object, TimeProvider.System, _logger.Object);

        var result = await handler.HandleAsync(new AtualizarAnamneseAlunoCommand(
            aluno.Id, Doencas: "Hipertensão", ConsentimentoDadosSaude: false));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("aluno.consentimento_saude_obrigatorio");
        _logAprovacaoRepo.Verify(r => r.AdicionarAsync(It.IsAny<LogAprovacao>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
