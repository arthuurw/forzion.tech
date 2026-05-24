using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Tests.Domain.Entities;

public class ContaRecebimentoTests
{
    private static readonly Guid TreinadorId = Guid.NewGuid();

    [Fact]
    public void Criar_TreinadorIdValido_CriaPendente()
    {
        var conta = ContaRecebimento.Criar(TreinadorId);

        conta.TreinadorId.Should().Be(TreinadorId);
        conta.StripeConnectAccountId.Should().BeNull();
        conta.OnboardingCompleto.Should().BeFalse();
        conta.Configurada.Should().BeFalse();
    }

    [Fact]
    public void Criar_TreinadorIdVazio_LancaDomainException()
    {
        var act = () => ContaRecebimento.Criar(Guid.Empty);
        act.Should().Throw<DomainException>().WithMessage("O identificador do treinador é inválido.");
    }

    // --- ConfigurarStripeConnect ---

    [Fact]
    public void ConfigurarStripeConnect_AccountIdValido_Salva()
    {
        var conta = ContaRecebimento.Criar(TreinadorId);

        conta.ConfigurarStripeConnect("acct_123");

        conta.StripeConnectAccountId.Should().Be("acct_123");
        conta.Configurada.Should().BeTrue();
        conta.UpdatedAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ConfigurarStripeConnect_AccountIdVazio_LancaDomainException(string accountId)
    {
        var conta = ContaRecebimento.Criar(TreinadorId);
        var act = () => conta.ConfigurarStripeConnect(accountId);
        act.Should().Throw<DomainException>().WithMessage("O identificador da conta Stripe é inválido.");
    }

    // --- ConfirmarOnboarding ---

    [Fact]
    public void ConfirmarOnboarding_ComContaConfigurada_MarcaCompleto()
    {
        var conta = ContaRecebimento.Criar(TreinadorId);
        conta.ConfigurarStripeConnect("acct_123");

        conta.ConfirmarOnboarding();

        conta.OnboardingCompleto.Should().BeTrue();
        conta.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void ConfirmarOnboarding_SemContaConfigurada_LancaDomainException()
    {
        var conta = ContaRecebimento.Criar(TreinadorId);
        var act = () => conta.ConfirmarOnboarding();
        act.Should().Throw<DomainException>().WithMessage("O treinador não possui conta Stripe configurada.");
    }
}
