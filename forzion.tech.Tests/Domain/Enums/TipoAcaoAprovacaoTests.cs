using FluentAssertions;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Tests.Domain.Enums;

public class TipoAcaoAprovacaoTests
{
    [Fact]
    public void DefinicaoDadosFiscaisTreinador_PermaneceOnze()
    {
        ((int)TipoAcaoAprovacao.DefinicaoDadosFiscaisTreinador).Should().Be(11);
    }

    [Fact]
    public void DefinicaoCortesiaTreinador_PermaneceVinteCinco()
    {
        ((int)TipoAcaoAprovacao.DefinicaoCortesiaTreinador).Should().Be(25);
    }

    [Fact]
    public void NenhumMembro_UsaValorVinteQuatro()
    {
        Enum.GetValues<TipoAcaoAprovacao>().Select(v => (int)v).Should().NotContain(24);
    }
}
