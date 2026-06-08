using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Domain.Entities;

public class TreinadorTests
{
    private static readonly Guid ContaId = Guid.NewGuid();

    // --- Criar ---

    [Fact]
    public void Criar_DadosValidos_RetornaTreinador()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora).Value;

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
        var t = Treinador.Criar(ContaId, "  Carlos  ", TestData.Agora).Value;
        t.Nome.Should().Be("Carlos");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_NomeVazio_LancaDomainException(string nome)
    {
        var r = Treinador.Criar(ContaId, nome, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O nome é obrigatório.");
    }

    [Fact]
    public void Criar_ContaIdVazio_LancaDomainException()
    {
        var r = Treinador.Criar(Guid.Empty, "Carlos", TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O identificador da conta é inválido.");
    }

    [Fact]
    public void Criar_NomeMuitoLongo_LancaDomainException()
    {
        var r = Treinador.Criar(ContaId, new string('a', 101), TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O nome deve ter no máximo 100 caracteres.");
    }

    // --- Aprovar ---

    [Fact]
    public void Aprovar_AguardandoAprovacao_MudaParaAtivo()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora).Value;
        var adminId = Guid.NewGuid();

        t.Aprovar(adminId, TestData.Agora);

        t.Status.Should().Be(TreinadorStatus.Ativo);
        t.AprovadoPorId.Should().Be(adminId);
        t.AprovadoEm.Should().NotBeNull();
        t.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Aprovar_JaAtivo_LancaDomainException()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora).Value;
        t.Aprovar(Guid.NewGuid(), TestData.Agora);

        var r = t.Aprovar(Guid.NewGuid(), TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("Apenas treinadores aguardando aprovação podem ser aprovados.");
    }

    // --- Inativar ---

    [Fact]
    public void Inativar_Ativo_MudaParaInativo()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora).Value;
        t.Aprovar(Guid.NewGuid(), TestData.Agora);

        t.Inativar(TestData.Agora);

        t.Status.Should().Be(TreinadorStatus.Inativo);
        t.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Inativar_JaInativo_LancaDomainException()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora).Value;
        t.Aprovar(Guid.NewGuid(), TestData.Agora);
        t.Inativar(TestData.Agora);

        var r = t.Inativar(TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O treinador já está inativo.");
    }

    // --- AtribuirPlano ---

    [Fact]
    public void AtribuirPlano_PlanoValido_AtribuiId()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora).Value;
        var planoId = Guid.NewGuid();

        t.AtribuirPlano(planoId, TestData.Agora);

        t.PlanoPlataformaId.Should().Be(planoId);
        t.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void AtribuirPlano_IdVazio_LancaDomainException()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora).Value;
        var r = t.AtribuirPlano(Guid.Empty, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O identificador do plano é inválido.");
    }

    // --- Reprovar ---

    [Fact]
    public void Reprovar_AguardandoAprovacao_MudaParaInativo()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora).Value;
        var adminId = Guid.NewGuid();

        t.Reprovar(adminId, TestData.Agora);

        t.Status.Should().Be(TreinadorStatus.Inativo);
        t.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Reprovar_JaAtivo_LancaDomainException()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora).Value;
        t.Aprovar(Guid.NewGuid(), TestData.Agora);

        var r = t.Reprovar(Guid.NewGuid(), TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("Apenas treinadores aguardando aprovação podem ser reprovados.");
    }

    // --- ValidarDisponibilidade ---

    [Fact]
    public void ValidarDisponibilidade_Ativo_NaoLancaExcecao()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora).Value;
        t.Aprovar(Guid.NewGuid(), TestData.Agora);

        var r = t.ValidarDisponibilidade();
        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidarDisponibilidade_AguardandoAprovacao_LancaDomainException()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora).Value;

        var r = t.ValidarDisponibilidade();
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O treinador selecionado não está disponível.");
    }

    // --- ValidarParaExclusao ---

    [Fact]
    public void ValidarParaExclusao_Inativo_NaoLancaExcecao()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora).Value;
        t.Aprovar(Guid.NewGuid(), TestData.Agora);
        t.Inativar(TestData.Agora);

        var r = t.ValidarParaExclusao();
        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidarParaExclusao_Ativo_LancaDomainException()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora).Value;
        t.Aprovar(Guid.NewGuid(), TestData.Agora);

        var r = t.ValidarParaExclusao();
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("Apenas treinadores inativos podem ser excluídos permanentemente.");
    }

    // --- AtualizarNome ---

    [Fact]
    public void AtualizarNome_DadosValidos_AtualizaNomeEUpdatedAt()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora).Value;
        t.AtualizarNome("  João  ", TestData.Agora);
        t.Nome.Should().Be("João");
        t.UpdatedAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AtualizarNome_NomeVazio_LancaDomainException(string nome)
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora).Value;
        var r = t.AtualizarNome(nome, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O nome não pode ser vazio.");
    }

    [Fact]
    public void AtualizarNome_NomeMuitoLongo_LancaDomainException()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora).Value;
        var r = t.AtualizarNome(new string('a', 101), TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O nome deve ter no máximo 100 caracteres.");
    }

    // --- AtribuirPlano (guard inativo) ---

    [Fact]
    public void AtribuirPlano_TreinadorInativo_LancaDomainException()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora).Value;
        t.Aprovar(Guid.NewGuid(), TestData.Agora);
        t.Inativar(TestData.Agora);

        var r = t.AtribuirPlano(Guid.NewGuid(), TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("Não é possível atribuir plano a um treinador inativo.");
    }

    // --- Criar com telefone ---

    [Fact]
    public void Criar_ComTelefone_SalvaTelefone()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora, "  11999999999  ").Value;
        t.Telefone.Should().Be("11999999999");
    }

    [Fact]
    public void Criar_TelefoneVazio_SalvaNull()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora, "   ").Value;
        t.Telefone.Should().BeNull();
    }

    // --- Plano + modo de pagamento (signup) ---

    [Fact]
    public void Criar_Default_AguardandoAprovacao_ModoPlataforma()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora).Value;

        t.Status.Should().Be(TreinadorStatus.AguardandoAprovacao);
        t.ModoPagamentoAluno.Should().Be(ModoPagamentoAluno.Plataforma);
        t.PlanoPlataformaId.Should().BeNull();
    }

    [Fact]
    public void Criar_PlanoPago_AguardandoPagamentoComPlanoEModo()
    {
        var planoId = Guid.NewGuid();

        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora, null, planoId, ModoPagamentoAluno.Externo, aguardandoPagamento: true).Value;

        t.Status.Should().Be(TreinadorStatus.AguardandoPagamento);
        t.PlanoPlataformaId.Should().Be(planoId);
        t.ModoPagamentoAluno.Should().Be(ModoPagamentoAluno.Externo);
    }

    [Fact]
    public void ConfirmarPagamentoPlano_AguardandoPagamento_VaiParaAguardandoAprovacao()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora, null, Guid.NewGuid(), ModoPagamentoAluno.Plataforma, aguardandoPagamento: true).Value;

        t.ConfirmarPagamentoPlano(TestData.Agora).IsSuccess.Should().BeTrue();
        t.Status.Should().Be(TreinadorStatus.AguardandoAprovacao);

        t.Aprovar(Guid.NewGuid(), TestData.Agora).IsSuccess.Should().BeTrue();
        t.Status.Should().Be(TreinadorStatus.Ativo);
    }

    [Fact]
    public void ConfirmarPagamentoPlano_NaoAguardandoPagamento_Falha()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora).Value;
        t.ConfirmarPagamentoPlano(TestData.Agora).IsFailure.Should().BeTrue();
    }

    // --- AlterarModoPagamento (cooldown) ---

    [Fact]
    public void AlterarModoPagamento_PrimeiraTroca_AlteraModoERegistraData()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora).Value;

        var r = t.AlterarModoPagamento(ModoPagamentoAluno.Externo, TestData.Agora);

        r.IsSuccess.Should().BeTrue();
        t.ModoPagamentoAluno.Should().Be(ModoPagamentoAluno.Externo);
        t.ModoPagamentoAlunoAlteradoEm.Should().Be(TestData.Agora);
    }

    [Fact]
    public void AlterarModoPagamento_MesmoModo_Falha()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora).Value;

        var r = t.AlterarModoPagamento(ModoPagamentoAluno.Plataforma, TestData.Agora);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("treinador.modo_inalterado");
    }

    [Fact]
    public void AlterarModoPagamento_DentroDoCooldown_Falha()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora).Value;
        t.AlterarModoPagamento(ModoPagamentoAluno.Externo, TestData.Agora);

        var r = t.AlterarModoPagamento(ModoPagamentoAluno.Plataforma, TestData.Agora.AddDays(89));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("treinador.cooldown_modo_pagamento");
        t.ModoPagamentoAluno.Should().Be(ModoPagamentoAluno.Externo);
    }

    [Fact]
    public void AlterarModoPagamento_AposCooldown_Permite()
    {
        var t = Treinador.Criar(ContaId, "Carlos", TestData.Agora).Value;
        t.AlterarModoPagamento(ModoPagamentoAluno.Externo, TestData.Agora);

        var r = t.AlterarModoPagamento(ModoPagamentoAluno.Plataforma, TestData.Agora.AddDays(90));

        r.IsSuccess.Should().BeTrue();
        t.ModoPagamentoAluno.Should().Be(ModoPagamentoAluno.Plataforma);
    }
}
