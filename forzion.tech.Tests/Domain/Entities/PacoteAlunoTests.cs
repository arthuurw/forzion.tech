using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Tests.Domain.Entities;

public class PacoteAlunoTests
{
    private static readonly Guid TreinadorId = Guid.NewGuid();

    // --- Criar ---

    [Fact]
    public void Criar_DadosValidos_RetornaPacote()
    {
        var p = PacoteAluno.Criar(TreinadorId, "Pacote A", 3, 99.90m);

        p.Id.Should().NotBeEmpty();
        p.TreinadorId.Should().Be(TreinadorId);
        p.Nome.Should().Be("Pacote A");
        p.MaxFichas.Should().Be(3);
        p.Preco.Should().Be(99.90m);
        p.IsAtivo.Should().BeTrue();
    }

    [Fact]
    public void Criar_TreinadorIdVazio_LancaDomainException()
    {
        var act = () => PacoteAluno.Criar(Guid.Empty, "Nome", 3, 0);
        act.Should().Throw<DomainException>().WithMessage("O identificador do treinador é inválido.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_NomeVazio_LancaDomainException(string nome)
    {
        var act = () => PacoteAluno.Criar(TreinadorId, nome, 3, 0);
        act.Should().Throw<DomainException>().WithMessage("O nome é obrigatório.");
    }

    [Fact]
    public void Criar_MaxFichasZero_LancaDomainException()
    {
        var act = () => PacoteAluno.Criar(TreinadorId, "Nome", 0, 0);
        act.Should().Throw<DomainException>().WithMessage("O limite de fichas deve ser maior que zero.");
    }

    [Fact]
    public void Criar_PrecoNegativo_LancaDomainException()
    {
        var act = () => PacoteAluno.Criar(TreinadorId, "Nome", 1, -1);
        act.Should().Throw<DomainException>().WithMessage("O preço não pode ser negativo.");
    }

    // --- Atualizar ---

    [Fact]
    public void Atualizar_ComNome_AtualizaNome()
    {
        var p = PacoteAluno.Criar(TreinadorId, "A", 1, 0);
        p.Atualizar("B", null, null);
        p.Nome.Should().Be("B");
        p.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Atualizar_ComMaxFichas_AtualizaMaxFichas()
    {
        var p = PacoteAluno.Criar(TreinadorId, "A", 1, 0);
        p.Atualizar(null, 5, null);
        p.MaxFichas.Should().Be(5);
    }

    [Fact]
    public void Atualizar_CamposNulos_NaoAltera()
    {
        var p = PacoteAluno.Criar(TreinadorId, "A", 3, 50);
        p.Atualizar(null, null, null);
        p.Nome.Should().Be("A");
        p.MaxFichas.Should().Be(3);
        p.Preco.Should().Be(50);
    }

    // --- Inativar ---

    [Fact]
    public void Inativar_Ativo_MudaParaInativo()
    {
        var p = PacoteAluno.Criar(TreinadorId, "A", 1, 0);
        p.Inativar();
        p.IsAtivo.Should().BeFalse();
        p.UpdatedAt.Should().NotBeNull();
    }
}
