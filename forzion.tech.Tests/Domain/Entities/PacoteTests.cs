using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Domain.Entities;

public class PacoteTests
{
    private static readonly Guid TreinadorId = Guid.NewGuid();

    // --- Criar ---

    [Fact]
    public void Criar_DadosValidos_RetornaPacote()
    {
        var p = Pacote.Criar(TreinadorId, "Pacote A", 99.90m, TestData.Agora, "Treino + whatsapp");

        p.Id.Should().NotBeEmpty();
        p.TreinadorId.Should().Be(TreinadorId);
        p.Nome.Should().Be("Pacote A");
        p.Descricao.Should().Be("Treino + whatsapp");
        p.Preco.Should().Be(99.90m);
        p.IsAtivo.Should().BeTrue();
    }

    [Fact]
    public void Criar_SemDescricao_DescricaoNula()
    {
        var p = Pacote.Criar(TreinadorId, "Pacote A", 99.90m, TestData.Agora);
        p.Descricao.Should().BeNull();
    }

    [Fact]
    public void Criar_TreinadorIdVazio_LancaDomainException()
    {
        var act = () => Pacote.Criar(Guid.Empty, "Nome", 0, TestData.Agora);
        act.Should().Throw<DomainException>().WithMessage("O identificador do treinador é inválido.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_NomeVazio_LancaDomainException(string nome)
    {
        var act = () => Pacote.Criar(TreinadorId, nome, 0, TestData.Agora);
        act.Should().Throw<DomainException>().WithMessage("O nome é obrigatório.");
    }

    [Fact]
    public void Criar_PrecoNegativo_LancaDomainException()
    {
        var act = () => Pacote.Criar(TreinadorId, "Nome", -1, TestData.Agora);
        act.Should().Throw<DomainException>().WithMessage("O preço não pode ser negativo.");
    }

    [Fact]
    public void Criar_DescricaoMuitoLonga_LancaDomainException()
    {
        var descricaoLonga = new string('x', 501);
        var act = () => Pacote.Criar(TreinadorId, "Nome", 0, TestData.Agora, descricaoLonga);
        act.Should().Throw<DomainException>().WithMessage("A descrição deve ter no máximo 500 caracteres.");
    }

    // --- Atualizar ---

    [Fact]
    public void Atualizar_ComNome_AtualizaNome()
    {
        var p = Pacote.Criar(TreinadorId, "A", 0, TestData.Agora);
        p.Atualizar("B", null, null);
        p.Nome.Should().Be("B");
        p.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Atualizar_ComDescricao_AtualizaDescricao()
    {
        var p = Pacote.Criar(TreinadorId, "A", 0, TestData.Agora);
        p.Atualizar(null, null, "Premium com vídeo");
        p.Descricao.Should().Be("Premium com vídeo");
        p.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Atualizar_ComPreco_AtualizaPreco()
    {
        var p = Pacote.Criar(TreinadorId, "A", 50, TestData.Agora);
        p.Atualizar(null, 120m, null);
        p.Preco.Should().Be(120m);
    }

    [Fact]
    public void Atualizar_CamposNulos_NaoAltera()
    {
        var p = Pacote.Criar(TreinadorId, "A", 50, TestData.Agora, "desc");
        p.Atualizar(null, null, null);
        p.Nome.Should().Be("A");
        p.Descricao.Should().Be("desc");
        p.Preco.Should().Be(50);
    }

    [Fact]
    public void Atualizar_DescricaoMuitoLonga_LancaDomainException()
    {
        var p = Pacote.Criar(TreinadorId, "A", 0, TestData.Agora);
        var descricaoLonga = new string('x', 501);
        var act = () => p.Atualizar(null, null, descricaoLonga);
        act.Should().Throw<DomainException>().WithMessage("A descrição deve ter no máximo 500 caracteres.");
    }

    // --- Inativar ---

    [Fact]
    public void Inativar_Ativo_MudaParaInativo()
    {
        var p = Pacote.Criar(TreinadorId, "A", 0, TestData.Agora);
        p.Inativar();
        p.IsAtivo.Should().BeFalse();
        p.UpdatedAt.Should().NotBeNull();
    }
}
