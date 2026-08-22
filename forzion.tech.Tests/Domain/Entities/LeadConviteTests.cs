using FluentAssertions;
using forzion.tech.Domain.Entities;

namespace forzion.tech.Tests.Domain.Entities;

public class LeadConviteTests
{
    private static readonly DateTime Agora = new(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid LeadId = Guid.NewGuid();
    private static readonly Guid TreinadorId = Guid.NewGuid();
    private const string HashValido = "hash-do-token";

    [Fact]
    public void Criar_DadosValidos_RetornaConviteCorreto()
    {
        var expiraEm = Agora.AddDays(14);

        var r = LeadConvite.Criar(LeadId, TreinadorId, HashValido, expiraEm, Agora);

        r.IsSuccess.Should().BeTrue();
        r.Value.Id.Should().NotBe(Guid.Empty);
        r.Value.LeadId.Should().Be(LeadId);
        r.Value.TreinadorId.Should().Be(TreinadorId);
        r.Value.TokenHash.Should().Be(HashValido);
        r.Value.ExpiraEm.Should().Be(expiraEm);
        r.Value.CreatedAt.Should().Be(Agora);
        r.Value.UsadoEm.Should().BeNull();
        r.Value.InvalidadoEm.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_TokenHashVazio_Falha(string hash)
    {
        var r = LeadConvite.Criar(LeadId, TreinadorId, hash, Agora.AddDays(14), Agora);

        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Criar_ExpiracaoNaoFutura_Falha()
    {
        var r = LeadConvite.Criar(LeadId, TreinadorId, HashValido, Agora, Agora);

        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Consumir_ConviteValido_MarcaUsadoEm()
    {
        var convite = LeadConvite.Criar(LeadId, TreinadorId, HashValido, Agora.AddDays(14), Agora).Value;

        var r = convite.Consumir(Agora.AddDays(1));

        r.IsSuccess.Should().BeTrue();
        convite.UsadoEm.Should().Be(Agora.AddDays(1));
    }

    [Fact]
    public void Consumir_JaUtilizado_Falha()
    {
        var convite = LeadConvite.Criar(LeadId, TreinadorId, HashValido, Agora.AddDays(14), Agora).Value;
        convite.Consumir(Agora.AddDays(1));

        var r = convite.Consumir(Agora.AddDays(2));

        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Consumir_Invalidado_Falha()
    {
        var convite = LeadConvite.Criar(LeadId, TreinadorId, HashValido, Agora.AddDays(14), Agora).Value;
        convite.Invalidar(Agora.AddDays(1));

        var r = convite.Consumir(Agora.AddDays(2));

        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Consumir_Expirado_Falha()
    {
        var convite = LeadConvite.Criar(LeadId, TreinadorId, HashValido, Agora.AddDays(14), Agora).Value;

        var r = convite.Consumir(Agora.AddDays(15));

        r.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Invalidar_ChamadoDuasVezes_EIdempotente()
    {
        var convite = LeadConvite.Criar(LeadId, TreinadorId, HashValido, Agora.AddDays(14), Agora).Value;

        var r1 = convite.Invalidar(Agora.AddDays(1));
        var primeiraInvalidacao = convite.InvalidadoEm;
        var r2 = convite.Invalidar(Agora.AddDays(2));

        r1.IsSuccess.Should().BeTrue();
        r2.IsSuccess.Should().BeTrue();
        convite.InvalidadoEm.Should().Be(primeiraInvalidacao);
    }

    [Fact]
    public void Invalidar_ImpedeConsumoPosterior()
    {
        var convite = LeadConvite.Criar(LeadId, TreinadorId, HashValido, Agora.AddDays(14), Agora).Value;
        convite.Invalidar(Agora.AddDays(1));

        var r = convite.Consumir(Agora.AddDays(1));

        r.IsFailure.Should().BeTrue();
        convite.UsadoEm.Should().BeNull();
    }
}
