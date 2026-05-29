using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Domain.Entities;

public class ContaRecebimentoTests
{
    private static readonly Guid TreinadorId = Guid.NewGuid();

    [Fact]
    public void Criar_TreinadorIdValido_CriaPendente()
    {
        var conta = ContaRecebimento.Criar(TreinadorId, TestData.Agora).Value;

        conta.TreinadorId.Should().Be(TreinadorId);
        conta.StripeConnectAccountId.Should().BeNull();
        conta.OnboardingCompleto.Should().BeFalse();
        conta.Configurada.Should().BeFalse();
    }

    [Fact]
    public void Criar_TreinadorIdVazio_LancaDomainException()
    {
        var r = ContaRecebimento.Criar(Guid.Empty, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O identificador do treinador é inválido.");
    }

    // --- ConfigurarStripeConnect ---

    [Fact]
    public void ConfigurarStripeConnect_AccountIdValido_Salva()
    {
        var conta = ContaRecebimento.Criar(TreinadorId, TestData.Agora).Value;

        conta.ConfigurarStripeConnect("acct_123", TestData.Agora);

        conta.StripeConnectAccountId.Should().Be("acct_123");
        conta.Configurada.Should().BeTrue();
        conta.UpdatedAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ConfigurarStripeConnect_AccountIdVazio_LancaDomainException(string accountId)
    {
        var conta = ContaRecebimento.Criar(TreinadorId, TestData.Agora).Value;
        var r = conta.ConfigurarStripeConnect(accountId, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O identificador da conta Stripe é inválido.");
    }

    // --- ConfirmarOnboarding ---

    [Fact]
    public void ConfirmarOnboarding_ComContaConfigurada_MarcaCompleto()
    {
        var conta = ContaRecebimento.Criar(TreinadorId, TestData.Agora).Value;
        conta.ConfigurarStripeConnect("acct_123", TestData.Agora);

        conta.ConfirmarOnboarding(TestData.Agora);

        conta.OnboardingCompleto.Should().BeTrue();
        conta.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void ConfirmarOnboarding_SemContaConfigurada_LancaDomainException()
    {
        var conta = ContaRecebimento.Criar(TreinadorId, TestData.Agora).Value;
        var r = conta.ConfirmarOnboarding(TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O treinador não possui conta Stripe configurada.");
    }
}
