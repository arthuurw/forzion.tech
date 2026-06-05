using FluentAssertions;
using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Treinadores.RegistrarTreinador;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Application.Treinadores;

public class RegistrarTreinadorHandlerTests
{
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IPlanoPlataformaRepository> _planoRepo = new();
    private readonly Mock<IAssinaturaTreinadorRepository> _assinaturaRepo = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<RegistrarTreinadorHandler>> _logger = new();
    private readonly RegistrarTreinadorHandler _handler;
    private Conta? _contaAdicionada;

    public RegistrarTreinadorHandlerTests()
    {
        _passwordHasher.Setup(p => p.Hash(It.IsAny<string>())).Returns("hash");
        _contaRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Conta?)null);
        _contaRepo.Setup(r => r.AdicionarAsync(It.IsAny<Conta>(), It.IsAny<CancellationToken>()))
            .Callback<Conta, CancellationToken>((c, _) => _contaAdicionada = c)
            .Returns(Task.CompletedTask);
        _handler = new RegistrarTreinadorHandler(
            _contaRepo.Object, _treinadorRepo.Object, _planoRepo.Object, _assinaturaRepo.Object,
            _passwordHasher.Object, _unitOfWork.Object, new RegistrarTreinadorCommandValidator(), TimeProvider.System, _logger.Object);
    }

    private Guid SetupPlano(TierPlano tier, decimal preco, bool ativo = true)
    {
        var plano = PlanoPlataforma.Criar(tier.ToString(), tier, 25, preco, DateTime.UtcNow).Value;
        if (!ativo) plano.Inativar(DateTime.UtcNow);
        _planoRepo.Setup(r => r.ObterPorIdAsync(plano.Id, It.IsAny<CancellationToken>())).ReturnsAsync(plano);
        return plano.Id;
    }

    private RegistrarTreinadorCommand Cmd(Guid planoId, ModoPagamentoAluno modo = ModoPagamentoAluno.Plataforma)
        => new("ana@teste.com", "Senha123", "Ana", planoId, modo);

    [Fact]
    public async Task HandleAsync_PlanoFree_AguardandoAprovacao_SemAssinatura_EnviaVerificacao()
    {
        var planoId = SetupPlano(TierPlano.Free, 0m);

        var result = await _handler.HandleAsync(Cmd(planoId));

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(TreinadorStatus.AguardandoAprovacao);
        _assinaturaRepo.Verify(r => r.AdicionarAsync(It.IsAny<AssinaturaTreinador>(), It.IsAny<CancellationToken>()), Times.Never);
        _contaAdicionada!.DomainEvents.OfType<ContaRegistradaEvent>().Should().ContainSingle("Free envia verificação no cadastro");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PlanoPago_AguardandoPagamento_CriaAssinatura_SemVerificacaoAinda()
    {
        var planoId = SetupPlano(TierPlano.Basic, 50m);

        var result = await _handler.HandleAsync(Cmd(planoId, ModoPagamentoAluno.Externo));

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(TreinadorStatus.AguardandoPagamento);
        _assinaturaRepo.Verify(r => r.AdicionarAsync(It.Is<AssinaturaTreinador>(a => a.Valor == 50m && a.Status == AssinaturaTreinadorStatus.Pendente), It.IsAny<CancellationToken>()), Times.Once);
        _contaAdicionada!.DomainEvents.OfType<ContaRegistradaEvent>().Should().BeEmpty("plano pago só envia verificação após o pagamento");
    }

    [Fact]
    public async Task HandleAsync_PlanoElite_Rejeitado()
    {
        var planoId = SetupPlano(TierPlano.Elite, 500m);
        var result = await _handler.HandleAsync(Cmd(planoId));
        result.IsFailure.Should().BeTrue();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PlanoInativo_Rejeitado()
    {
        var planoId = SetupPlano(TierPlano.Basic, 50m, ativo: false);
        var result = await _handler.HandleAsync(Cmd(planoId));
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("plano_inativo");
    }

    [Fact]
    public async Task HandleAsync_PlanoNaoEncontrado_LancaException()
    {
        var act = async () => await _handler.HandleAsync(Cmd(Guid.NewGuid()));
        await act.Should().ThrowAsync<PlanoPlataformaNaoEncontradoException>();
    }

    [Fact]
    public async Task HandleAsync_EmailJaCadastrado_LancaException()
    {
        var conta = Conta.Criar(Email.Criar("ana@teste.com").Value, "hash", TipoConta.Treinador, DateTime.UtcNow).Value;
        _contaRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(conta);

        var act = async () => await _handler.HandleAsync(Cmd(Guid.NewGuid()));
        await act.Should().ThrowAsync<EmailJaCadastradoException>();
    }

    [Fact]
    public async Task HandleAsync_DadosInvalidos_LancaValidationException()
    {
        var act = async () => await _handler.HandleAsync(new RegistrarTreinadorCommand("invalido", "123", "", Guid.NewGuid(), ModoPagamentoAluno.Plataforma));
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_PlanoIdVazio_LancaValidationException()
    {
        var act = async () => await _handler.HandleAsync(new RegistrarTreinadorCommand("ana@teste.com", "Senha123", "Ana", Guid.Empty, ModoPagamentoAluno.Plataforma));
        await act.Should().ThrowAsync<ValidationException>();
    }
}
