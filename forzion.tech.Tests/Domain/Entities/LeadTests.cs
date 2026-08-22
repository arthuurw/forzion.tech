using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Tests.Domain.Entities;

public class LeadTests
{
    private static readonly DateTime Agora = new(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TreinadorId = Guid.NewGuid();

    private static ContatoLead ContatoValido() =>
        ContatoLead.Criar(TipoContatoLead.Email, "lead@example.com").Value;

    private static ConsentimentoLead ConsentimentoValido() =>
        ConsentimentoLead.Criar("Contato comercial", Agora, Agora).Value;

    [Fact]
    public void Criar_ComDadosValidos_NasceNovoENaoAnonimizado()
    {
        var r = Lead.Criar(TreinadorId, "Fulano", ContatoValido(), "quero treinar", ConsentimentoValido(), null, LeadSource.Agent, "chave-1", "hash-1", Agora);

        r.IsSuccess.Should().BeTrue();
        r.Value.Status.Should().Be(LeadStatus.Novo);
        r.Value.Anonimizado.Should().BeFalse();
        r.Value.UltimoToqueEm.Should().Be(Agora);
        r.Value.CreatedAt.Should().Be(Agora);
        r.Value.TreinadorId.Should().Be(TreinadorId);
        r.Value.Nome.Should().Be("Fulano");
        r.Value.Source.Should().Be(LeadSource.Agent);
        r.Value.IdempotencyKey.Should().Be("chave-1");
        r.Value.ArgumentosHash.Should().Be("hash-1");
    }

    [Fact]
    public void Criar_EmiteLeadCriadoEvent()
    {
        var r = Lead.Criar(TreinadorId, "Fulano", ContatoValido(), null, ConsentimentoValido(), null, LeadSource.Manual, null, null, Agora);

        r.Value.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<LeadCriadoEvent>()
            .Which.Should().BeEquivalentTo(new LeadCriadoEvent(r.Value.Id, TreinadorId, LeadSource.Manual, Agora));
    }

    [Fact]
    public void Criar_TreinadorIdVazio_Falha()
    {
        var r = Lead.Criar(Guid.Empty, "Fulano", ContatoValido(), null, ConsentimentoValido(), null, LeadSource.Agent, null, null, Agora);

        r.IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_NomeAusente_Falha(string nome)
    {
        var r = Lead.Criar(TreinadorId, nome, ContatoValido(), null, ConsentimentoValido(), null, LeadSource.Agent, null, null, Agora);

        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Criar_NomeAcimaDe200_Falha()
    {
        var nome = new string('a', 201);

        var r = Lead.Criar(TreinadorId, nome, ContatoValido(), null, ConsentimentoValido(), null, LeadSource.Agent, null, null, Agora);

        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Contain("200");
    }

    [Fact]
    public void Criar_InteresseAcimaDe1000_Falha()
    {
        var interesse = new string('a', 1001);

        var r = Lead.Criar(TreinadorId, "Fulano", ContatoValido(), interesse, ConsentimentoValido(), null, LeadSource.Agent, null, null, Agora);

        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Contain("1000");
    }

    [Fact]
    public void Criar_SemIdempotencyKeyNemHash_AceitaComoNulo()
    {
        var r = Lead.Criar(TreinadorId, "Fulano", ContatoValido(), null, ConsentimentoValido(), null, LeadSource.Manual, null, null, Agora);

        r.IsSuccess.Should().BeTrue();
        r.Value.IdempotencyKey.Should().BeNull();
        r.Value.ArgumentosHash.Should().BeNull();
    }
}
