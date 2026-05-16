using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Tests.Domain.Entities;

public class AlunoTests
{
    private static readonly Guid ContaId = Guid.NewGuid();

    // --- Criar ---

    [Fact]
    public void Criar_ComDadosMinimos_RetornaAluno()
    {
        var aluno = Aluno.Criar(ContaId, "João");

        aluno.Id.Should().NotBeEmpty();
        aluno.Nome.Should().Be("João");
        aluno.ContaId.Should().Be(ContaId);
        aluno.Status.Should().Be(AlunoStatus.AguardandoAprovacao);
        aluno.Email.Should().BeNull();
        aluno.Telefone.Should().BeNull();
    }

    [Fact]
    public void Criar_ComEmailETelefone_Preenche()
    {
        var aluno = Aluno.Criar(ContaId, "João", "joao@email.com", "11999999999");

        aluno.Email?.Value.Should().Be("joao@email.com");
        aluno.Telefone.Should().Be("11999999999");
    }

    [Fact]
    public void Criar_NomeComEspacos_Remove()
    {
        var aluno = Aluno.Criar(ContaId, "  João  ");
        aluno.Nome.Should().Be("João");
    }

    [Fact]
    public void Criar_DefinCreatedAt()
    {
        var antes = DateTime.UtcNow;
        var aluno = Aluno.Criar(ContaId, "João");
        aluno.CreatedAt.Should().BeOnOrAfter(antes);
        aluno.UpdatedAt.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_NomeVazio_LancaDomainException(string nome)
    {
        var act = () => Aluno.Criar(ContaId, nome);
        act.Should().Throw<DomainException>().WithMessage("O nome é obrigatório.");
    }

    [Fact]
    public void Criar_NomeMuitoLongo_LancaDomainException()
    {
        var act = () => Aluno.Criar(ContaId, new string('a', 101));
        act.Should().Throw<DomainException>().WithMessage("O nome deve ter no máximo 100 caracteres.");
    }

    [Fact]
    public void Criar_ContaIdVazio_LancaDomainException()
    {
        var act = () => Aluno.Criar(Guid.Empty, "João");
        act.Should().Throw<DomainException>().WithMessage("O identificador da conta é inválido.");
    }

    [Fact]
    public void Criar_EmailInvalido_LancaDomainException()
    {
        var act = () => Aluno.Criar(ContaId, "João", email: "invalido");
        act.Should().Throw<DomainException>().WithMessage("O e-mail informado é inválido.");
    }

    [Fact]
    public void Criar_TelefoneMuitoLongo_LancaDomainException()
    {
        var act = () => Aluno.Criar(ContaId, "João", telefone: new string('9', 21));
        act.Should().Throw<DomainException>().WithMessage("O telefone deve ter no máximo 20 caracteres.");
    }

    // --- Atualizar ---

    [Fact]
    public void Atualizar_ComNome_AtualizaNome()
    {
        var aluno = Aluno.Criar(ContaId, "João");
        aluno.Atualizar("Maria", null, null);
        aluno.Nome.Should().Be("Maria");
    }

    [Fact]
    public void Atualizar_ComEmail_AtualizaEmail()
    {
        var aluno = Aluno.Criar(ContaId, "João");
        aluno.Atualizar(null, "novo@email.com", null);
        aluno.Email?.Value.Should().Be("novo@email.com");
    }

    [Fact]
    public void Atualizar_ComEmailVazio_LimpaEmail()
    {
        var aluno = Aluno.Criar(ContaId, "João", "joao@email.com");
        aluno.Atualizar(null, "", null);
        aluno.Email.Should().BeNull();
    }

    [Fact]
    public void Atualizar_ComTelefoneVazio_LimpaTelefone()
    {
        var aluno = Aluno.Criar(ContaId, "João", telefone: "11999");
        aluno.Atualizar(null, null, "");
        aluno.Telefone.Should().BeNull();
    }

    [Fact]
    public void Atualizar_ComCamposNulos_NaoAltera()
    {
        var aluno = Aluno.Criar(ContaId, "João");
        aluno.Atualizar(null, null, null);
        aluno.Nome.Should().Be("João");
    }

    [Fact]
    public void Atualizar_DefinUpdatedAt()
    {
        var aluno = Aluno.Criar(ContaId, "João");
        var antes = DateTime.UtcNow;
        aluno.Atualizar("Maria", null, null);
        aluno.UpdatedAt.Should().BeOnOrAfter(antes);
    }

    [Fact]
    public void Atualizar_NomeVazio_LancaDomainException()
    {
        var aluno = Aluno.Criar(ContaId, "João");
        var act = () => aluno.Atualizar("", null, null);
        act.Should().Throw<DomainException>().WithMessage("O nome não pode ser vazio.");
    }

    // --- Ativar / Inativar ---

    [Fact]
    public void Ativar_DeAguardandoAprovacao_AlteraStatusParaAtivo()
    {
        var aluno = Aluno.Criar(ContaId, "João");
        aluno.Ativar();
        aluno.Status.Should().Be(AlunoStatus.Ativo);
    }

    [Fact]
    public void Ativar_DeInativo_AlteraStatusParaAtivo()
    {
        var aluno = Aluno.Criar(ContaId, "João");
        aluno.Ativar();
        aluno.Inativar();
        aluno.Ativar();
        aluno.Status.Should().Be(AlunoStatus.Ativo);
    }

    [Fact]
    public void Ativar_JaAtivo_LancaDomainException()
    {
        var aluno = Aluno.Criar(ContaId, "João");
        aluno.Ativar();
        var act = () => aluno.Ativar();
        act.Should().Throw<DomainException>().WithMessage("O aluno já está ativo.");
    }

    [Fact]
    public void Inativar_DeAtivo_AlteraStatusParaInativo()
    {
        var aluno = Aluno.Criar(ContaId, "João");
        aluno.Ativar();
        aluno.Inativar();
        aluno.Status.Should().Be(AlunoStatus.Inativo);
    }

    [Fact]
    public void Inativar_JaInativo_LancaDomainException()
    {
        var aluno = Aluno.Criar(ContaId, "João");
        aluno.Ativar();
        aluno.Inativar();
        var act = () => aluno.Inativar();
        act.Should().Throw<DomainException>().WithMessage("O aluno já está inativo.");
    }

    [Fact]
    public void Ativar_DefinUpdatedAt()
    {
        var aluno = Aluno.Criar(ContaId, "João");
        var antes = DateTime.UtcNow;
        aluno.Ativar();
        aluno.UpdatedAt.Should().BeOnOrAfter(antes);
    }
}
