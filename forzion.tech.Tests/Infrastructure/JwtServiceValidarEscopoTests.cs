using FluentAssertions;
using forzion.tech.Application.Auth;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;

namespace forzion.tech.Tests.Infrastructure;

public class JwtServiceValidarEscopoTests
{
    private static readonly DateTime Agora = new(2026, 6, 17, 12, 0, 0, DateTimeKind.Utc);

    private static JwtService CriarServico(TimeProvider time) =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:JwtSecret"] = "forzion-test-secret-key-32-bytes!!",
                ["Auth:JwtIssuer"] = "forzion.tech",
                ["Auth:JwtAudience"] = "forzion.tech",
            })
            .Build(), time);

    private static Conta Conta() =>
        forzion.tech.Domain.Entities.Conta.Criar(Email.Criar("user@test.com").Value, "hash", TipoConta.Aluno, Agora).Value;

    [Fact]
    public void ValidarTokenEscopo_TokenStepUpValido_RetornaIdentidade()
    {
        var servico = CriarServico(TimeProvider.System);
        var conta = Conta();
        var emitido = servico.GerarTokenEscopo(conta, MfaScopes.StepUp, TimeSpan.FromMinutes(5));

        var validado = servico.ValidarTokenEscopo(emitido.Token, MfaScopes.StepUp);

        validado.Should().NotBeNull();
        validado!.ContaId.Should().Be(conta.Id);
        validado.Jti.Should().Be(emitido.Jti);
    }

    [Fact]
    public void ValidarTokenEscopo_EscopoDivergente_RetornaNull()
    {
        var servico = CriarServico(TimeProvider.System);
        var emitido = servico.GerarTokenEscopo(Conta(), MfaScopes.StepUp, TimeSpan.FromMinutes(5));

        servico.ValidarTokenEscopo(emitido.Token, MfaScopes.Pendente).Should().BeNull();
    }

    [Fact]
    public void ValidarTokenEscopo_TokenAdulterado_RetornaNull()
    {
        var servico = CriarServico(TimeProvider.System);
        var emitido = servico.GerarTokenEscopo(Conta(), MfaScopes.StepUp, TimeSpan.FromMinutes(5));

        servico.ValidarTokenEscopo(emitido.Token + "x", MfaScopes.StepUp).Should().BeNull();
    }

    [Fact]
    public void ValidarTokenEscopo_TokenExpirado_RetornaNull()
    {
        var servicoNoPassado = CriarServico(new FakeTimeProvider(new DateTimeOffset(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc))));
        var emitido = servicoNoPassado.GerarTokenEscopo(Conta(), MfaScopes.StepUp, TimeSpan.FromMinutes(5));

        servicoNoPassado.ValidarTokenEscopo(emitido.Token, MfaScopes.StepUp).Should().BeNull();
    }
}
