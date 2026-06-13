using FluentAssertions;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Suporte.EnviarMensagem;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using Moq;

namespace forzion.tech.Tests.Application.Suporte;

public class EnviarMensagemSuporteHandlerTests
{
    private readonly Mock<IUserContext> _userContext = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<IMensagemSuporteRepository> _mensagemRepo = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly EnviarMensagemSuporteHandler _handler;

    private static readonly Guid ContaId = Guid.NewGuid();
    private const string AssuntoValido = "Dúvida sobre fichas";
    private const string DescricaoValida = "Não consigo visualizar minha ficha de treino desta semana.";

    public EnviarMensagemSuporteHandlerTests()
    {
        // Validator real: a validação faz parte do comportamento sob teste.
        _handler = new EnviarMensagemSuporteHandler(
            _userContext.Object,
            _contaRepo.Object,
            _mensagemRepo.Object,
            _unitOfWork.Object,
            TimeProvider.System,
            new EnviarMensagemSuporteCommandValidator());
    }

    private static Conta ContaFake() =>
        Conta.Criar(Email.Criar("user@forzion.tech").Value, "hash", TipoConta.Aluno, DateTime.UtcNow).Value;

    private EnviarMensagemSuporteCommand ComandoValido(string categoria = "Duvida") =>
        new(categoria, AssuntoValido, DescricaoValida);

    [Fact]
    public async Task HandleAsync_DadosValidos_PersisteEComita()
    {
        MensagemSuporte? adicionada = null;
        _userContext.Setup(u => u.ContaId).Returns(ContaId);
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>())).ReturnsAsync(ContaFake());
        _mensagemRepo.Setup(r => r.AdicionarAsync(It.IsAny<MensagemSuporte>(), It.IsAny<CancellationToken>()))
            .Callback<MensagemSuporte, CancellationToken>((m, _) => adicionada = m)
            .Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(ComandoValido("Sugestao"));

        result.IsSuccess.Should().BeTrue();
        adicionada.Should().NotBeNull();
        adicionada!.ContaId.Should().Be(ContaId);
        adicionada.Categoria.Should().Be(CategoriaSuporte.Sugestao);
        adicionada.Assunto.Should().Be(AssuntoValido);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CategoriaCaseInsensitive_Mapeada()
    {
        MensagemSuporte? adicionada = null;
        _userContext.Setup(u => u.ContaId).Returns(ContaId);
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>())).ReturnsAsync(ContaFake());
        _mensagemRepo.Setup(r => r.AdicionarAsync(It.IsAny<MensagemSuporte>(), It.IsAny<CancellationToken>()))
            .Callback<MensagemSuporte, CancellationToken>((m, _) => adicionada = m)
            .Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(ComandoValido("outro"));

        result.IsSuccess.Should().BeTrue();
        adicionada!.Categoria.Should().Be(CategoriaSuporte.Outro);
    }

    [Fact]
    public async Task HandleAsync_ContaInexistente_Falha_NaoComita()
    {
        _userContext.Setup(u => u.ContaId).Returns(ContaId);
        _contaRepo.Setup(r => r.ObterPorIdAsync(ContaId, It.IsAny<CancellationToken>())).ReturnsAsync((Conta?)null);

        var result = await _handler.HandleAsync(ComandoValido());

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("conta.nao_encontrada");
        _mensagemRepo.Verify(r => r.AdicionarAsync(It.IsAny<MensagemSuporte>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("Invalida", "assunto bom", "descricao com tamanho suficiente para passar limite")]
    [InlineData("Duvida", "ab", "descricao com tamanho suficiente para passar limite")]
    [InlineData("Duvida", "assunto bom", "curta")]
    public async Task HandleAsync_DadosInvalidos_LancaValidationException(string categoria, string assunto, string descricao)
    {
        _userContext.Setup(u => u.ContaId).Returns(ContaId);

        var act = async () => await _handler.HandleAsync(new EnviarMensagemSuporteCommand(categoria, assunto, descricao));

        await act.Should().ThrowAsync<ValidationException>();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
