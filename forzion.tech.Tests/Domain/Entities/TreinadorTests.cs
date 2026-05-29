using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Domain.Entities;

public class TreinadorTests
{
    private static readonly Guid ContaId = Guid.NewGuid();

    // --- Criar ---

    [Fact]
    public void Criar_DadosValidos_RetornaTreinador()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora);

        t.Id.Should().NotBeEmpty();
        t.ContaId.Should().Be(ContaId);
        t.Nome.Should().Be("Carlos");
        t.Status.Should().Be(TreinadorStatus.AguardandoAprovacao);
        t.PlanoPlataformaId.Should().BeNull();
        t.AprovadoPorId.Should().BeNull();
        t.AprovadoEm.Should().BeNull();
    }

    [Fact]
    public void Criar_NomeComEspacos_Remove()
    {
        var t = Treinador.Criar(ContaId, "  Carlos  ", TestData.Agora);
        t.Nome.Should().Be("Carlos");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_NomeVazio_LancaDomainException(string nome)
    {
        var act = () => Treinador.Criar(ContaId, nome, TestData.Agora);
        act.Should().Throw<DomainException>().WithMessage("O nome é obrigatório.");
    }

    [Fact]
    public void Criar_ContaIdVazio_LancaDomainException()
    {
        var act = () => Treinador.Criar(Guid.Empty, "Carlos", TestData.Agora);
        act.Should().Throw<DomainException>().WithMessage("O identificador da conta é inválido.");
    }

    [Fact]
    public void Criar_NomeMuitoLongo_LancaDomainException()
    {
        var act = () => Treinador.Criar(ContaId, new string('a', 101), TestData.Agora);
        act.Should().Throw<DomainException>().WithMessage("O nome deve ter no máximo 100 caracteres.");
    }

    // --- Aprovar ---

    [Fact]
    public void Aprovar_AguardandoAprovacao_MudaParaAtivo()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora);
        var adminId = Guid.NewGuid();

        t.Aprovar(adminId);

        t.Status.Should().Be(TreinadorStatus.Ativo);
        t.AprovadoPorId.Should().Be(adminId);
        t.AprovadoEm.Should().NotBeNull();
        t.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Aprovar_JaAtivo_LancaDomainException()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora);
        t.Aprovar(Guid.NewGuid());

        var act = () => t.Aprovar(Guid.NewGuid());
        act.Should().Throw<DomainException>().WithMessage("Apenas treinadores aguardando aprovação podem ser aprovados.");
    }

    // --- Inativar ---

    [Fact]
    public void Inativar_Ativo_MudaParaInativo()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora);
        t.Aprovar(Guid.NewGuid());

        t.Inativar();

        t.Status.Should().Be(TreinadorStatus.Inativo);
        t.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Inativar_JaInativo_LancaDomainException()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora);
        t.Aprovar(Guid.NewGuid());
        t.Inativar();

        var act = () => t.Inativar();
        act.Should().Throw<DomainException>().WithMessage("O treinador já está inativo.");
    }

    // --- AtribuirPlano ---

    [Fact]
    public void AtribuirPlano_PlanoValido_AtribuiId()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora);
        var planoId = Guid.NewGuid();

        t.AtribuirPlano(planoId);

        t.PlanoPlataformaId.Should().Be(planoId);
        t.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void AtribuirPlano_IdVazio_LancaDomainException()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora);
        var act = () => t.AtribuirPlano(Guid.Empty);
        act.Should().Throw<DomainException>().WithMessage("O identificador do plano é inválido.");
    }

    // --- Reprovar ---

    [Fact]
    public void Reprovar_AguardandoAprovacao_MudaParaInativo()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora);
        var adminId = Guid.NewGuid();

        t.Reprovar(adminId);

        t.Status.Should().Be(TreinadorStatus.Inativo);
        t.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Reprovar_JaAtivo_LancaDomainException()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora);
        t.Aprovar(Guid.NewGuid());

        var act = () => t.Reprovar(Guid.NewGuid());
        act.Should().Throw<DomainException>().WithMessage("Apenas treinadores aguardando aprovação podem ser reprovados.");
    }

    // --- ValidarDisponibilidade ---

    [Fact]
    public void ValidarDisponibilidade_Ativo_NaoLancaExcecao()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora);
        t.Aprovar(Guid.NewGuid());

        var act = () => t.ValidarDisponibilidade();
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidarDisponibilidade_AguardandoAprovacao_LancaDomainException()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora);

        var act = () => t.ValidarDisponibilidade();
        act.Should().Throw<DomainException>().WithMessage("O treinador selecionado não está disponível.");
    }

    // --- ValidarParaExclusao ---

    [Fact]
    public void ValidarParaExclusao_Inativo_NaoLancaExcecao()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora);
        t.Aprovar(Guid.NewGuid());
        t.Inativar();

        var act = () => t.ValidarParaExclusao();
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidarParaExclusao_Ativo_LancaDomainException()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora);
        t.Aprovar(Guid.NewGuid());

        var act = () => t.ValidarParaExclusao();
        act.Should().Throw<DomainException>().WithMessage("Apenas treinadores inativos podem ser excluídos permanentemente.");
    }

    // --- AtualizarNome ---

    [Fact]
    public void AtualizarNome_DadosValidos_AtualizaNomeEUpdatedAt()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora);
        t.AtualizarNome("  João  ");
        t.Nome.Should().Be("João");
        t.UpdatedAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AtualizarNome_NomeVazio_LancaDomainException(string nome)
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora);
        var act = () => t.AtualizarNome(nome);
        act.Should().Throw<DomainException>().WithMessage("O nome não pode ser vazio.");
    }

    [Fact]
    public void AtualizarNome_NomeMuitoLongo_LancaDomainException()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora);
        var act = () => t.AtualizarNome(new string('a', 101));
        act.Should().Throw<DomainException>().WithMessage("O nome deve ter no máximo 100 caracteres.");
    }

    // --- AtribuirPlano (guard inativo) ---

    [Fact]
    public void AtribuirPlano_TreinadorInativo_LancaDomainException()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora);
        t.Aprovar(Guid.NewGuid());
        t.Inativar();

        var act = () => t.AtribuirPlano(Guid.NewGuid());
        act.Should().Throw<DomainException>().WithMessage("Não é possível atribuir plano a um treinador inativo.");
    }

    // --- Criar com telefone ---

    [Fact]
    public void Criar_ComTelefone_SalvaTelefone()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora, "  11999999999  ");
        t.Telefone.Should().Be("11999999999");
    }

    [Fact]
    public void Criar_TelefoneVazio_SalvaNull()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora, "   ");
        t.Telefone.Should().BeNull();
    }
}
