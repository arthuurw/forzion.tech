using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Tests.Domain.Entities;

public class AlunoTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid TreinadorId = Guid.NewGuid();

    // --- Criar ---

    [Fact]
    public void Criar_ComDadosMinimos_RetornaAluno()
    {
        var aluno = Aluno.Criar("João", TenantId, TreinadorId);

        aluno.Id.Should().NotBeEmpty();
        aluno.Nome.Should().Be("João");
        aluno.TenantId.Should().Be(TenantId);
        aluno.TreinadorId.Should().Be(TreinadorId);
        aluno.Status.Should().Be(AlunoStatus.Ativo);
        aluno.Email.Should().BeNull();
        aluno.Telefone.Should().BeNull();
    }

    [Fact]
    public void Criar_ComEmailETelefone_Preenche()
    {
        var aluno = Aluno.Criar("João", TenantId, TreinadorId, "joao@email.com", "11999999999");

        aluno.Email.Should().Be("joao@email.com");
        aluno.Telefone.Should().Be("11999999999");
    }

    [Fact]
    public void Criar_NomeComEspacos_Remove()
    {
        var aluno = Aluno.Criar("  João  ", TenantId, TreinadorId);
        aluno.Nome.Should().Be("João");
    }

    [Fact]
    public void Criar_DefinCreatedAt()
    {
        var antes = DateTime.UtcNow;
        var aluno = Aluno.Criar("João", TenantId, TreinadorId);
        aluno.CreatedAt.Should().BeOnOrAfter(antes);
        aluno.UpdatedAt.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_NomeVazio_LancaDomainException(string nome)
    {
        var act = () => Aluno.Criar(nome, TenantId, TreinadorId);
        act.Should().Throw<DomainException>().WithMessage("O nome é obrigatório.");
    }

    [Fact]
    public void Criar_NomeMuitoLongo_LancaDomainException()
    {
        var act = () => Aluno.Criar(new string('a', 101), TenantId, TreinadorId);
        act.Should().Throw<DomainException>().WithMessage("O nome deve ter no máximo 100 caracteres.");
    }

    [Fact]
    public void Criar_TenantIdVazio_LancaDomainException()
    {
        var act = () => Aluno.Criar("João", Guid.Empty, TreinadorId);
        act.Should().Throw<DomainException>().WithMessage("O tenant é inválido.");
    }

    [Fact]
    public void Criar_TreinadorIdVazio_LancaDomainException()
    {
        var act = () => Aluno.Criar("João", TenantId, Guid.Empty);
        act.Should().Throw<DomainException>().WithMessage("O treinador é inválido.");
    }

    [Fact]
    public void Criar_EmailInvalido_LancaDomainException()
    {
        var act = () => Aluno.Criar("João", TenantId, TreinadorId, email: "invalido");
        act.Should().Throw<DomainException>().WithMessage("O e-mail informado é inválido.");
    }

    [Fact]
    public void Criar_TelefoneMuitoLongo_LancaDomainException()
    {
        var act = () => Aluno.Criar("João", TenantId, TreinadorId, telefone: new string('9', 21));
        act.Should().Throw<DomainException>().WithMessage("O telefone deve ter no máximo 20 caracteres.");
    }

    // --- Atualizar ---

    [Fact]
    public void Atualizar_ComNome_AtualizaNome()
    {
        var aluno = Aluno.Criar("João", TenantId, TreinadorId);
        aluno.Atualizar("Maria", null, null);
        aluno.Nome.Should().Be("Maria");
    }

    [Fact]
    public void Atualizar_ComEmail_AtualizaEmail()
    {
        var aluno = Aluno.Criar("João", TenantId, TreinadorId);
        aluno.Atualizar(null, "novo@email.com", null);
        aluno.Email.Should().Be("novo@email.com");
    }

    [Fact]
    public void Atualizar_ComEmailVazio_LimpaEmail()
    {
        var aluno = Aluno.Criar("João", TenantId, TreinadorId, "joao@email.com");
        aluno.Atualizar(null, "", null);
        aluno.Email.Should().BeNull();
    }

    [Fact]
    public void Atualizar_ComTelefoneVazio_LimpaTelefone()
    {
        var aluno = Aluno.Criar("João", TenantId, TreinadorId, telefone: "11999");
        aluno.Atualizar(null, null, "");
        aluno.Telefone.Should().BeNull();
    }

    [Fact]
    public void Atualizar_ComCamposNulos_NaoAltera()
    {
        var aluno = Aluno.Criar("João", TenantId, TreinadorId);
        aluno.Atualizar(null, null, null);
        aluno.Nome.Should().Be("João");
    }

    [Fact]
    public void Atualizar_DefinUpdatedAt()
    {
        var aluno = Aluno.Criar("João", TenantId, TreinadorId);
        var antes = DateTime.UtcNow;
        aluno.Atualizar("Maria", null, null);
        aluno.UpdatedAt.Should().BeOnOrAfter(antes);
    }

    [Fact]
    public void Atualizar_NomeVazio_LancaDomainException()
    {
        var aluno = Aluno.Criar("João", TenantId, TreinadorId);
        var act = () => aluno.Atualizar("", null, null);
        act.Should().Throw<DomainException>().WithMessage("O nome não pode ser vazio.");
    }

    // --- AlterarStatus ---

    [Fact]
    public void AlterarStatus_ParaInativo_AlteraStatus()
    {
        var aluno = Aluno.Criar("João", TenantId, TreinadorId);
        aluno.AlterarStatus(AlunoStatus.Inativo);
        aluno.Status.Should().Be(AlunoStatus.Inativo);
    }

    [Fact]
    public void AlterarStatus_DefinUpdatedAt()
    {
        var aluno = Aluno.Criar("João", TenantId, TreinadorId);
        var antes = DateTime.UtcNow;
        aluno.AlterarStatus(AlunoStatus.Inativo);
        aluno.UpdatedAt.Should().BeOnOrAfter(antes);
    }
}
