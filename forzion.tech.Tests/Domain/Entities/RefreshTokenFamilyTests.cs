using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Tests.Domain.Entities;

public class RefreshTokenFamilyTests
{
    [Fact]
    public void Criar_DadosValidos_RetornaFamiliaAtiva()
    {
        var contaId = Guid.NewGuid();
        var agora = DateTime.UtcNow;

        var familia = RefreshTokenFamily.Criar(contaId, agora.AddDays(90), agora, "Chrome/Android").Value;

        familia.ContaId.Should().Be(contaId);
        familia.RevogadaEm.Should().BeNull();
        familia.Rotulo.Should().Be("Chrome/Android");
        familia.EstaAtiva(agora).Should().BeTrue();
    }

    [Fact]
    public void Criar_ContaVazia_Falha()
    {
        var agora = DateTime.UtcNow;
        var r = RefreshTokenFamily.Criar(Guid.Empty, agora.AddDays(90), agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("refresh.conta_id_invalido");
    }

    [Fact]
    public void Criar_AbsolutoNaoFuturo_Falha()
    {
        var agora = DateTime.UtcNow;
        var r = RefreshTokenFamily.Criar(Guid.NewGuid(), agora, agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("refresh.absoluto_nao_futuro");
    }

    [Fact]
    public void Revogar_FamiliaAtiva_MarcaRevogadaComMotivo()
    {
        var agora = DateTime.UtcNow;
        var familia = RefreshTokenFamily.Criar(Guid.NewGuid(), agora.AddDays(90), agora).Value;

        var r = familia.Revogar(MotivoRevogacaoFamilia.ReuseDetectado, agora.AddMinutes(5));

        r.IsSuccess.Should().BeTrue();
        familia.RevogadaEm.Should().Be(agora.AddMinutes(5));
        familia.MotivoRevogacao.Should().Be(MotivoRevogacaoFamilia.ReuseDetectado);
        familia.EstaAtiva(agora.AddMinutes(5)).Should().BeFalse();
    }

    [Fact]
    public void Revogar_FamiliaJaRevogada_Falha()
    {
        var agora = DateTime.UtcNow;
        var familia = RefreshTokenFamily.Criar(Guid.NewGuid(), agora.AddDays(90), agora).Value;
        familia.Revogar(MotivoRevogacaoFamilia.Logout, agora).IsSuccess.Should().BeTrue();

        var r = familia.Revogar(MotivoRevogacaoFamilia.Admin, agora);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("refresh.familia_ja_revogada");
    }

    [Fact]
    public void EstaAtiva_AposAbsoluto_Falsa()
    {
        var agora = DateTime.UtcNow;
        var familia = RefreshTokenFamily.Criar(Guid.NewGuid(), agora.AddDays(90), agora).Value;

        familia.EstaAtiva(agora.AddDays(91)).Should().BeFalse();
    }
}
