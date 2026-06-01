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
    private static readonly Email EmailValido = Email.Criar("treinador@email.com").Value;
    private const string HashValido = "$2a$12$abcdefghijklmnopqrstuuVGrfHSMr6yp6vQI1234567890abcdef";

    [Fact]
    public void Criar_DadosValidos_RetornaContaCorreta()
    {
        var conta = Conta.Criar(EmailValido, HashValido, TipoConta.Treinador, TestData.Agora).Value;

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
        var conta = Conta.Criar(EmailValido, HashValido, TipoConta.Aluno, TestData.Agora).Value;

        conta.EmailVerificado.Should().BeFalse();
        conta.VerificadoEm.Should().BeNull();
    }

    [Fact]
    public void Criar_DadosValidos_DispararaContaRegistradaEvent()
    {
        var agora = new DateTime(2026, 5, 25, 12, 0, 0, DateTimeKind.Utc);

        var conta = Conta.Criar(EmailValido, HashValido, TipoConta.Aluno, agora).Value;

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
        var conta = Conta.Criar(EmailValido, HashValido, tipo, TestData.Agora).Value;
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
        var r = Conta.Criar(EmailValido, hash, TipoConta.Treinador, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Contain("hash da senha");
    }

    [Fact]
    public void AtualizarSenha_NovoHashValido_AtualizaEPreencheUpdatedAt()
    {
        var conta = Conta.Criar(EmailValido, HashValido, TipoConta.Aluno, TestData.Agora).Value;
        var novoHash = "$2a$12$zyxwvutsrqponmlkjihgffeKLMNOPQRSTUVWXYZ0123456789abcd";

        var r = conta.AtualizarSenha(novoHash, TestData.Agora);

        r.IsSuccess.Should().BeTrue();
        conta.PasswordHash.Should().Be(novoHash);
        conta.UpdatedAt.Should().Be(TestData.Agora);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AtualizarSenha_HashVazio_LancaDomainException(string hash)
    {
        var conta = Conta.Criar(EmailValido, HashValido, TipoConta.Treinador, TestData.Agora).Value;
        var r = conta.AtualizarSenha(hash, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Contain("hash da senha");
    }

    [Fact]
    public void MarcarEmailVerificado_ContaNaoVerificada_PreencheFlagEDatas()
    {
        var agora = new DateTime(2026, 5, 25, 12, 0, 0, DateTimeKind.Utc);
        var conta = Conta.Criar(EmailValido, HashValido, TipoConta.Aluno, TestData.Agora).Value;

        conta.MarcarEmailVerificado(agora);

        conta.EmailVerificado.Should().BeTrue();
        conta.VerificadoEm.Should().Be(agora);
        conta.UpdatedAt.Should().Be(agora);
    }

    [Fact]
    public void MarcarEmailVerificado_ContaJaVerificada_NaoAlteraDatas()
    {
        var primeiraVez = new DateTime(2026, 5, 25, 12, 0, 0, DateTimeKind.Utc);
        var conta = Conta.Criar(EmailValido, HashValido, TipoConta.Aluno, TestData.Agora).Value;
        conta.MarcarEmailVerificado(primeiraVez);

        conta.MarcarEmailVerificado(primeiraVez.AddHours(1));

        conta.EmailVerificado.Should().BeTrue();
        conta.VerificadoEm.Should().Be(primeiraVez);
        conta.UpdatedAt.Should().Be(primeiraVez);
    }
}
