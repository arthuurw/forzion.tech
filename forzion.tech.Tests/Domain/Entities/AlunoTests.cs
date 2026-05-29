using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Domain.Entities;

public class AlunoTests
{
    private static readonly Guid ContaId = Guid.NewGuid();

    // --- Criar ---

    [Fact]
    public void Criar_ComDadosMinimos_RetornaAluno()
    {
        var aluno = Aluno.Criar(ContaId, "João", TestData.Agora).Value;

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
        var aluno = Aluno.Criar(ContaId, "João", TestData.Agora, "joao@email.com", "11999999999").Value;

        aluno.Email?.Value.Should().Be("joao@email.com");
        aluno.Telefone.Should().Be("11999999999");
    }

    [Fact]
    public void Criar_NomeComEspacos_Remove()
    {
        var aluno = Aluno.Criar(ContaId, "  João  ", TestData.Agora).Value;
        aluno.Nome.Should().Be("João");
    }

    [Fact]
    public void Criar_DefinCreatedAt()
    {
        var antes = TestData.Agora;
        var aluno = Aluno.Criar(ContaId, "João", TestData.Agora).Value;
        aluno.CreatedAt.Should().BeOnOrAfter(antes);
        aluno.UpdatedAt.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_NomeVazio_LancaDomainException(string nome)
    {
        var r = Aluno.Criar(ContaId, nome, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O nome é obrigatório.");
    }

    [Fact]
    public void Criar_NomeMuitoLongo_LancaDomainException()
    {
        var r = Aluno.Criar(ContaId, new string('a', 101), TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O nome deve ter no máximo 100 caracteres.");
    }

    [Fact]
    public void Criar_ContaIdVazio_LancaDomainException()
    {
        var r = Aluno.Criar(Guid.Empty, "João", TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O identificador da conta é inválido.");
    }

    [Fact]
    public void Criar_EmailInvalido_LancaDomainException()
    {
        var r = Aluno.Criar(ContaId, "João", TestData.Agora, email: "invalido");
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O e-mail informado é inválido.");
    }

    [Fact]
    public void Criar_TelefoneMuitoLongo_LancaDomainException()
    {
        var r = Aluno.Criar(ContaId, "João", TestData.Agora, telefone: new string('9', 21));
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O telefone deve ter no máximo 20 caracteres.");
    }

    // --- Atualizar ---

    [Fact]
    public void Atualizar_ComNome_AtualizaNome()
    {
        var aluno = Aluno.Criar(ContaId, "João", TestData.Agora).Value;
        aluno.Atualizar("Maria", null, null, TestData.Agora);
        aluno.Nome.Should().Be("Maria");
    }

    [Fact]
    public void Atualizar_ComEmail_AtualizaEmail()
    {
        var aluno = Aluno.Criar(ContaId, "João", TestData.Agora).Value;
        aluno.Atualizar(null, "novo@email.com", null, TestData.Agora);
        aluno.Email?.Value.Should().Be("novo@email.com");
    }

    [Fact]
    public void Atualizar_ComEmailVazio_LimpaEmail()
    {
        var aluno = Aluno.Criar(ContaId, "João", TestData.Agora, "joao@email.com").Value;
        aluno.Atualizar(null, "", null, TestData.Agora);
        aluno.Email.Should().BeNull();
    }

    [Fact]
    public void Atualizar_ComTelefoneVazio_LimpaTelefone()
    {
        var aluno = Aluno.Criar(ContaId, "João", TestData.Agora, telefone: "11999").Value;
        aluno.Atualizar(null, null, "", TestData.Agora);
        aluno.Telefone.Should().BeNull();
    }

    [Fact]
    public void Atualizar_ComCamposNulos_NaoAltera()
    {
        var aluno = Aluno.Criar(ContaId, "João", TestData.Agora).Value;
        aluno.Atualizar(null, null, null, TestData.Agora);
        aluno.Nome.Should().Be("João");
    }

    [Fact]
    public void Atualizar_DefinUpdatedAt()
    {
        var aluno = Aluno.Criar(ContaId, "João", TestData.Agora).Value;
        var antes = TestData.Agora;
        aluno.Atualizar("Maria", null, null, TestData.Agora);
        aluno.UpdatedAt.Should().BeOnOrAfter(antes);
    }

    [Fact]
    public void Atualizar_NomeVazio_LancaDomainException()
    {
        var aluno = Aluno.Criar(ContaId, "João", TestData.Agora).Value;
        var r = aluno.Atualizar("", null, null, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O nome não pode ser vazio.");
    }

    // --- Ativar / Inativar ---

    [Fact]
    public void Ativar_DeAguardandoAprovacao_AlteraStatusParaAtivo()
    {
        var aluno = Aluno.Criar(ContaId, "João", TestData.Agora).Value;
        aluno.Ativar(TestData.Agora);
        aluno.Status.Should().Be(AlunoStatus.Ativo);
    }

    [Fact]
    public void Ativar_DeInativo_AlteraStatusParaAtivo()
    {
        var aluno = Aluno.Criar(ContaId, "João", TestData.Agora).Value;
        aluno.Ativar(TestData.Agora);
        aluno.Inativar(TestData.Agora);
        aluno.Ativar(TestData.Agora);
        aluno.Status.Should().Be(AlunoStatus.Ativo);
    }

    [Fact]
    public void Ativar_JaAtivo_LancaDomainException()
    {
        var aluno = Aluno.Criar(ContaId, "João", TestData.Agora).Value;
        aluno.Ativar(TestData.Agora);
        var r = aluno.Ativar(TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O aluno já está ativo.");
    }

    [Fact]
    public void Inativar_DeAtivo_AlteraStatusParaInativo()
    {
        var aluno = Aluno.Criar(ContaId, "João", TestData.Agora).Value;
        aluno.Ativar(TestData.Agora);
        aluno.Inativar(TestData.Agora);
        aluno.Status.Should().Be(AlunoStatus.Inativo);
    }

    [Fact]
    public void Inativar_JaInativo_LancaDomainException()
    {
        var aluno = Aluno.Criar(ContaId, "João", TestData.Agora).Value;
        aluno.Ativar(TestData.Agora);
        aluno.Inativar(TestData.Agora);
        var r = aluno.Inativar(TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O aluno já está inativo.");
    }

    [Fact]
    public void Ativar_DefinUpdatedAt()
    {
        var aluno = Aluno.Criar(ContaId, "João", TestData.Agora).Value;
        var antes = TestData.Agora;
        aluno.Ativar(TestData.Agora);
        aluno.UpdatedAt.Should().BeOnOrAfter(antes);
    }
}
