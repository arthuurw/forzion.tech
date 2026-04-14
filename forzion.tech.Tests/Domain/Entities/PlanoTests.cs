using FluentAssertions;
using forzion.tech.Domain.Constants;
using forzion.tech.Domain.Entities;

namespace forzion.tech.Tests.Domain.Entities;

public class PlanoTests
{
    [Fact]
    public void Criar_ComDadosValidos_RetornaPlano()
    {
        var plano = Plano.Criar("Pro", 49.90m, 1000, false);

        plano.Should().NotBeNull();
        plano.Id.Should().NotBe(Guid.Empty);
        plano.Nome.Should().Be("Pro");
        plano.Preco.Should().Be(49.90m);
        plano.LimiteAlunos.Should().Be(1000);
        plano.IsFree.Should().BeFalse();
    }

    [Fact]
    public void Criar_SemIsFree_DefaultFalse()
    {
        var plano = Plano.Criar("Basic", 10m, 20);
        plano.IsFree.Should().BeFalse();
    }

    [Fact]
    public void Criar_GeraIdDiferente_CadaVez()
    {
        var plano1 = Plano.Criar("A", 0m, 5, true);
        var plano2 = Plano.Criar("A", 0m, 5, true);

        plano1.Id.Should().NotBe(plano2.Id);
    }

    [Fact]
    public void CriarComId_UsaIdFornecido()
    {
        var id = Guid.NewGuid();
        var plano = Plano.CriarComId(id, "Free", 0m, 5, true);

        plano.Id.Should().Be(id);
        plano.IsFree.Should().BeTrue();
    }

    [Fact]
    public void PlanoIds_ConstantesNaoSaoVazias()
    {
        PlanoIds.FreeId.Should().NotBe(Guid.Empty);
        PlanoIds.ProId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void PlanoIds_FreeEProSaoDiferentes()
    {
        PlanoIds.FreeId.Should().NotBe(PlanoIds.ProId);
    }
}
