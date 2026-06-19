using System.Text.Json;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Infrastructure.Notifications.Email;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Notifications.Email;

public class EmailCriticoDispatcherTests
{
    private readonly Mock<IOutboxEnfileirador> _outbox = new();
    private readonly IDataProtectionProvider _dataProtection =
        new ServiceCollection().AddDataProtection().Services.BuildServiceProvider()
            .GetRequiredService<IDataProtectionProvider>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 6, 18, 12, 0, 0, TimeSpan.Zero));
    private readonly EmailCriticoDispatcher _dispatcher;

    public EmailCriticoDispatcherTests()
    {
        _dispatcher = new EmailCriticoDispatcher(_outbox.Object, _dataProtection, _time);
    }

    [Fact]
    public void Enfileirar_EnfileiraEventoComTipoChaveECargaCifrada()
    {
        string? tipo = null;
        EmailCriticoSolicitadoEvent? evento = null;
        string? chave = null;
        _outbox.Setup(o => o.Enfileirar(It.IsAny<string>(), It.IsAny<EmailCriticoSolicitadoEvent>(), It.IsAny<string>()))
            .Callback<string, EmailCriticoSolicitadoEvent, string>((t, e, c) => (tipo, evento, chave) = (t, e, c));

        _dispatcher.Enfileirar(EmailCriticoTemplate.RedefinirSenha, "user@example.com", "segredo-raw");

        tipo.Should().Be($"evt:{typeof(EmailCriticoSolicitadoEvent).FullName}");
        evento.Should().NotBeNull();
        evento!.Template.Should().Be(EmailCriticoTemplate.RedefinirSenha);
        evento.OcorridoEm.Should().Be(_time.GetUtcNow().UtcDateTime);
        chave.Should().Be($"evt:EmailCritico:{evento.Id}");

        evento.DadosCifrados.Should().NotContain("user@example.com").And.NotContain("segredo-raw");
        var json = _dataProtection.CreateProtector(EmailCriticoDispatcher.ProtectorPurpose).Unprotect(evento.DadosCifrados);
        var dados = JsonSerializer.Deserialize<DadosEmailCritico>(json)!;
        dados.Destino.Should().Be("user@example.com");
        dados.Segredo.Should().Be("segredo-raw");
    }

    [Theory]
    [InlineData("", "s")]
    [InlineData("  ", "s")]
    [InlineData("d@e.com", "")]
    [InlineData("d@e.com", "  ")]
    public void Enfileirar_DestinoOuSegredoVazio_Lanca(string destino, string segredo)
    {
        var act = () => _dispatcher.Enfileirar(EmailCriticoTemplate.CodigoMfa, destino, segredo);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Enfileirar_ChavesDistintasPorChamada()
    {
        var chaves = new List<string>();
        _outbox.Setup(o => o.Enfileirar(It.IsAny<string>(), It.IsAny<EmailCriticoSolicitadoEvent>(), It.IsAny<string>()))
            .Callback<string, EmailCriticoSolicitadoEvent, string>((_, _, c) => chaves.Add(c));

        _dispatcher.Enfileirar(EmailCriticoTemplate.CodigoMfa, "d@e.com", "a");
        _dispatcher.Enfileirar(EmailCriticoTemplate.CodigoMfa, "d@e.com", "b");

        chaves.Should().HaveCount(2);
        chaves[0].Should().NotBe(chaves[1]);
    }
}
