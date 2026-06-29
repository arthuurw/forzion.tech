using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Domain.Entities;

public class ContaAnonimizarTests
{
    private static readonly Email EmailValido = Email.Criar("usuario@email.com").Value;
    private const string HashValido = "$2a$12$abcdefghijklmnopqrstuuVGrfHSMr6yp6vQI1234567890abcdef";

    private static Conta CriarConta(TipoConta tipo = TipoConta.Aluno)
        => Conta.Criar(EmailValido, HashValido, tipo, TestData.Agora).Value;

    [Fact]
    public void Anonimizar_PrimeiraVez_RetornaSuccess()
    {
        var conta = CriarConta();

        var resultado = conta.Anonimizar(TestData.Agora);

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Anonimizar_PrimeiraVez_ScrubaEmail()
    {
        var conta = CriarConta();

        conta.Anonimizar(TestData.Agora);

        conta.Email.Value.Should().EndWith("@anonimizado.local");
        conta.Email.Value.Should().StartWith("anon+");
        conta.Email.Value.Should().NotBe(EmailValido.Value);
    }

    [Fact]
    public void Anonimizar_PrimeiraVez_LimpaPasswordHash()
    {
        var conta = CriarConta();

        conta.Anonimizar(TestData.Agora);

        conta.PasswordHash.Should().BeEmpty();
    }

    [Fact]
    public void Anonimizar_PrimeiraVez_MarcaEmailNaoVerificado()
    {
        var conta = CriarConta();
        conta.MarcarEmailVerificado(TestData.Agora);

        conta.Anonimizar(TestData.Agora);

        conta.EmailVerificado.Should().BeFalse();
        conta.VerificadoEm.Should().BeNull();
    }

    [Fact]
    public void Anonimizar_PrimeiraVez_PreencheAnonimizadaEmEUpdatedAt()
    {
        var agora = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var conta = CriarConta();

        conta.Anonimizar(agora);

        conta.AnonimizadaEm.Should().Be(agora);
        conta.UpdatedAt.Should().Be(agora);
    }

    [Fact]
    public void Anonimizar_PrimeiraVez_DispararaContaAnonimizadaEvent()
    {
        var agora = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var conta = CriarConta(TipoConta.Treinador);
        conta.ClearDomainEvents();

        conta.Anonimizar(agora);

        conta.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ContaAnonimizadaEvent>()
            .Which.Should().BeEquivalentTo(new
            {
                ContaId = conta.Id,
                TipoConta = TipoConta.Treinador,
                OcorridoEm = agora,
            });
    }

    [Fact]
    public void Anonimizar_SegundaChamada_Idempotente_NaoReScruba()
    {
        var agora = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var conta = CriarConta();
        conta.Anonimizar(agora);

        var emailAposAnonimizacao = conta.Email.Value;
        conta.ClearDomainEvents();

        var resultado = conta.Anonimizar(agora.AddHours(1));

        resultado.IsSuccess.Should().BeTrue();
        conta.Email.Value.Should().Be(emailAposAnonimizacao);
        conta.AnonimizadaEm.Should().Be(agora);
        conta.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Anonimizar_SegundaChamada_Idempotente_NaoLancaNovosEventos()
    {
        var conta = CriarConta();
        conta.Anonimizar(TestData.Agora);
        conta.ClearDomainEvents();

        conta.Anonimizar(TestData.Agora.AddMinutes(5));

        conta.DomainEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData(TipoConta.Aluno)]
    [InlineData(TipoConta.Treinador)]
    [InlineData(TipoConta.SystemAdmin)]
    public void Anonimizar_TodosOsTipos_RegistraEventoComTipoCorreto(TipoConta tipo)
    {
        var conta = CriarConta(tipo);
        conta.ClearDomainEvents();

        conta.Anonimizar(TestData.Agora);

        var ev = conta.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ContaAnonimizadaEvent>().Subject;
        ev.TipoConta.Should().Be(tipo);
    }
}
