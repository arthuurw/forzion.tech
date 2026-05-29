using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Domain.Entities;

public class ContaTests
{
    private static readonly Email EmailValido = Email.Criar("treinador@email.com");
    private const string HashValido = "$2a$12$abcdefghijklmnopqrstuuVGrfHSMr6yp6vQI1234567890abcdef";

    [Fact]
    public void Criar_DadosValidos_RetornaContaCorreta()
    {
        var conta = Conta.Criar(EmailValido, HashValido, TipoConta.Treinador, TestData.Agora);

        conta.Id.Should().NotBe(Guid.Empty);
        conta.Email.Should().Be(EmailValido);
        conta.PasswordHash.Should().Be(HashValido);
        conta.TipoConta.Should().Be(TipoConta.Treinador);
        conta.CreatedAt.Should().BeCloseTo(TestData.Agora, TimeSpan.FromSeconds(2));
        conta.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Criar_DadosValidos_EmailNaoVerificado()
    {
        var conta = Conta.Criar(EmailValido, HashValido, TipoConta.Aluno, TestData.Agora);

        conta.EmailVerificado.Should().BeFalse();
        conta.VerificadoEm.Should().BeNull();
    }

    [Fact]
    public void Criar_DadosValidos_DispararaContaRegistradaEvent()
    {
        var agora = new DateTime(2026, 5, 25, 12, 0, 0, DateTimeKind.Utc);

        var conta = Conta.Criar(EmailValido, HashValido, TipoConta.Aluno, agora);

        conta.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ContaRegistradaEvent>()
            .Which.Should().BeEquivalentTo(new
            {
                ContaId = conta.Id,
                Email = EmailValido.Value,
                OcorridoEm = agora,
            });
    }

    [Theory]
    [InlineData(TipoConta.SystemAdmin)]
    [InlineData(TipoConta.Treinador)]
    [InlineData(TipoConta.Aluno)]
    public void Criar_TodosOsTipos_CriaCorretamente(TipoConta tipo)
    {
        var conta = Conta.Criar(EmailValido, HashValido, tipo, TestData.Agora);
        conta.TipoConta.Should().Be(tipo);
    }

    [Fact]
    public void Criar_EmailNulo_LancaArgumentNullException()
    {
        var act = () => Conta.Criar(null!, HashValido, TipoConta.Treinador, TestData.Agora);
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_PasswordHashVazio_LancaDomainException(string hash)
    {
        var act = () => Conta.Criar(EmailValido, hash, TipoConta.Treinador, TestData.Agora);
        act.Should().Throw<DomainException>().WithMessage("*hash da senha*");
    }

    [Fact]
    public void AtualizarSenha_NovoHashValido_AtualizaEPreencheUpdatedAt()
    {
        var conta = Conta.Criar(EmailValido, HashValido, TipoConta.Aluno, TestData.Agora);
        var novoHash = "$2a$12$zyxwvutsrqponmlkjihgffeKLMNOPQRSTUVWXYZ0123456789abcd";

        conta.AtualizarSenha(novoHash);

        conta.PasswordHash.Should().Be(novoHash);
        conta.UpdatedAt.Should().NotBeNull();
        // AtualizarSenha usa DateTime.UtcNow internamente — assertion contra UtcNow real.
        conta.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AtualizarSenha_HashVazio_LancaDomainException(string hash)
    {
        var conta = Conta.Criar(EmailValido, HashValido, TipoConta.Treinador, TestData.Agora);
        var act = () => conta.AtualizarSenha(hash);
        act.Should().Throw<DomainException>().WithMessage("*hash da senha*");
    }

    [Fact]
    public void MarcarEmailVerificado_ContaNaoVerificada_PreencheFlagEDatas()
    {
        var agora = new DateTime(2026, 5, 25, 12, 0, 0, DateTimeKind.Utc);
        var conta = Conta.Criar(EmailValido, HashValido, TipoConta.Aluno, TestData.Agora);

        conta.MarcarEmailVerificado(agora);

        conta.EmailVerificado.Should().BeTrue();
        conta.VerificadoEm.Should().Be(agora);
        conta.UpdatedAt.Should().Be(agora);
    }

    [Fact]
    public void MarcarEmailVerificado_ContaJaVerificada_NaoAlteraDatas()
    {
        var primeiraVez = new DateTime(2026, 5, 25, 12, 0, 0, DateTimeKind.Utc);
        var conta = Conta.Criar(EmailValido, HashValido, TipoConta.Aluno, TestData.Agora);
        conta.MarcarEmailVerificado(primeiraVez);

        conta.MarcarEmailVerificado(primeiraVez.AddHours(1));

        conta.EmailVerificado.Should().BeTrue();
        conta.VerificadoEm.Should().Be(primeiraVez);
        conta.UpdatedAt.Should().Be(primeiraVez);
    }
}
