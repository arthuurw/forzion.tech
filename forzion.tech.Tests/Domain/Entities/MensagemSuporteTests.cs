using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Domain.Entities;

public class MensagemSuporteTests
{
    private static readonly Guid ContaId = Guid.NewGuid();
    private const string AssuntoValido = "Dúvida sobre fichas";
    private const string DescricaoValida = "Não consigo visualizar minha ficha de treino desta semana.";

    [Fact]
    public void Criar_ComDadosValidos_RetornaMensagem()
    {
        var r = MensagemSuporte.Criar(ContaId, CategoriaSuporte.Duvida, AssuntoValido, DescricaoValida, TestData.Agora);

        r.IsSuccess.Should().BeTrue();
        var msg = r.Value;
        msg.Id.Should().NotBeEmpty();
        msg.ContaId.Should().Be(ContaId);
        msg.Categoria.Should().Be(CategoriaSuporte.Duvida);
        msg.Assunto.Should().Be(AssuntoValido);
        msg.Descricao.Should().Be(DescricaoValida);
        msg.CriadaEm.Should().Be(TestData.Agora);
    }

    [Fact]
    public void Criar_ComDadosValidos_RegistraEventoComSnapshot()
    {
        var msg = MensagemSuporte.Criar(ContaId, CategoriaSuporte.Sugestao, AssuntoValido, DescricaoValida, TestData.Agora).Value;

        var evento = msg.DomainEvents.OfType<MensagemSuporteCriadaEvent>().Should().ContainSingle().Subject;
        evento.MensagemSuporteId.Should().Be(msg.Id);
        evento.ContaId.Should().Be(ContaId);
        evento.Categoria.Should().Be(CategoriaSuporte.Sugestao);
        evento.Assunto.Should().Be(AssuntoValido);
        evento.Descricao.Should().Be(DescricaoValida);
    }

    [Fact]
    public void Criar_Trima_AssuntoEDescricao()
    {
        var msg = MensagemSuporte.Criar(ContaId, CategoriaSuporte.Outro, $"  {AssuntoValido}  ", $"  {DescricaoValida}  ", TestData.Agora).Value;
        msg.Assunto.Should().Be(AssuntoValido);
        msg.Descricao.Should().Be(DescricaoValida);
    }

    [Fact]
    public void Criar_ContaIdVazio_Falha()
    {
        var r = MensagemSuporte.Criar(Guid.Empty, CategoriaSuporte.Duvida, AssuntoValido, DescricaoValida, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("suporte.conta_id_invalido");
    }

    [Fact]
    public void Criar_CategoriaInvalida_Falha()
    {
        var r = MensagemSuporte.Criar(ContaId, (CategoriaSuporte)99, AssuntoValido, DescricaoValida, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("suporte.categoria_invalida");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_AssuntoVazio_Falha(string assunto)
    {
        var r = MensagemSuporte.Criar(ContaId, CategoriaSuporte.Duvida, assunto, DescricaoValida, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("suporte.assunto_obrigatorio");
    }

    [Theory]
    [InlineData(2)]
    [InlineData(121)]
    public void Criar_AssuntoForaDoTamanho_Falha(int tamanho)
    {
        var r = MensagemSuporte.Criar(ContaId, CategoriaSuporte.Duvida, new string('a', tamanho), DescricaoValida, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("suporte.assunto_tamanho");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_DescricaoVazia_Falha(string descricao)
    {
        var r = MensagemSuporte.Criar(ContaId, CategoriaSuporte.Duvida, AssuntoValido, descricao, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("suporte.descricao_obrigatoria");
    }

    [Theory]
    [InlineData(19)]
    [InlineData(2001)]
    public void Criar_DescricaoForaDoTamanho_Falha(int tamanho)
    {
        var r = MensagemSuporte.Criar(ContaId, CategoriaSuporte.Duvida, AssuntoValido, new string('a', tamanho), TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("suporte.descricao_tamanho");
    }
}
