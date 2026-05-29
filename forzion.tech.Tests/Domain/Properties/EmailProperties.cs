using CsCheck;
using FluentAssertions;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Tests.Domain.Properties;

/// <summary>
/// Properties (invariantes universais) do value object <see cref="Email"/>.
/// Os geradores sao alinhados ao regex do VO — nunca mais permissivos — para nao
/// gerar inputs que o VO aceitaria mas o regex rejeitaria.
/// </summary>
public class EmailProperties
{
    // Caracteres alfanumericos sao seguros: nao contem '@', espaco em branco nem '.'.
    private static readonly Gen<string> GenSegmento = Gen.String[Gen.Char.AlphaNumeric, 1, 12];

    // Email valido: local@dominio.tld — cada segmento sem '@'/espaco/'.'.
    private static readonly Gen<string> GenEmailValido =
        from local in GenSegmento
        from dominio in GenSegmento
        from tld in GenSegmento
        select $"{local}@{dominio}.{tld}";

    [Fact]
    public void Criar_EmailValido_NormalizaParaMinusculasSemEspacos()
    {
        GenEmailValido.Sample(raw =>
        {
            var email = Email.Criar(raw).Value;
            email.Value.Should().Be(raw.Trim().ToLowerInvariant());
        });
    }

    [Fact]
    public void Criar_ComEspacosAoRedor_IgnoraEspacos()
    {
        (from raw in GenEmailValido
         from prefixo in Gen.String[Gen.Char[" \t"], 0, 3]
         from sufixo in Gen.String[Gen.Char[" \t"], 0, 3]
         select (raw, padded: prefixo + raw + sufixo))
        .Sample(t =>
        {
            var email = Email.Criar(t.padded).Value;
            email.Value.Should().Be(t.raw.ToLowerInvariant());
        });
    }

    [Fact]
    public void Criar_Idempotente_ReaplicarNoValorNaoMuda()
    {
        GenEmailValido.Sample(raw =>
        {
            var primeira = Email.Criar(raw).Value;
            var segunda = Email.Criar(primeira.Value).Value;
            segunda.Value.Should().Be(primeira.Value);
        });
    }

    [Fact]
    public void Criar_EmailValido_SatisfazIgualdadeDeRecord()
    {
        GenEmailValido.Sample(raw =>
        {
            Email.Criar(raw).Value.Should().Be(Email.Criar(raw).Value);
        });
    }

    [Fact]
    public void Criar_SemArroba_LancaDomainException()
    {
        // Segmento alfanumerico sozinho nunca contem '@' nem '.' — sempre invalido.
        Gen.String[Gen.Char.AlphaNumeric, 1, 30].Sample(semArroba =>
        {
            Email.Criar(semArroba).IsFailure.Should().BeTrue();
        });
    }

    [Fact]
    public void Criar_EmBranco_LancaDomainException()
    {
        Gen.String[Gen.Char[" \t"], 0, 5].Sample(vazio =>
        {
            Email.Criar(vazio).IsFailure.Should().BeTrue();
        });
    }

    [Fact]
    public void Criar_AcimaDe256Caracteres_LancaDomainException()
    {
        // local muito longo garante > 256 apos normalizacao, mantendo formato valido.
        (from local in Gen.String[Gen.Char.AlphaNumeric, 257, 300]
         from dominio in GenSegmento
         from tld in GenSegmento
         select $"{local}@{dominio}.{tld}")
        .Sample(longo =>
        {
            var r = Email.Criar(longo);
            r.IsFailure.Should().BeTrue();
            r.Error!.Message.Should().Contain("256");
        });
    }
}
