using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Tests.Domain.Entities;

public class AssinaturaTests
{
    private static readonly Guid VinculoId = Guid.NewGuid();
    private static readonly Guid PacoteId = Guid.NewGuid();
    private static readonly Guid TreinadorId = Guid.NewGuid();
    private static readonly Guid AlunoId = Guid.NewGuid();
    private const decimal Valor = 150m;

    private static Assinatura CriarValida() =>
        Assinatura.Criar(VinculoId, PacoteId, TreinadorId, AlunoId, Valor);

    // --- Criar ---

    [Fact]
    public void Criar_DadosValidos_RetornaAssinaturaComStatusPendente()
    {
        var a = CriarValida();

        a.Id.Should().NotBeEmpty();
        a.VinculoId.Should().Be(VinculoId);
        a.PacoteAlunoId.Should().Be(PacoteId);
        a.TreinadorId.Should().Be(TreinadorId);
        a.AlunoId.Should().Be(AlunoId);
        a.Valor.Should().Be(Valor);
        a.Status.Should().Be(AssinaturaStatus.Pendente);
        a.DataCancelamento.Should().BeNull();
    }

    [Fact]
    public void Criar_DadosValidos_DispararaAssinaturaCriadaEvent()
    {
        var a = CriarValida();

        a.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<AssinaturaCriadaEvent>();
    }

    [Fact]
    public void Criar_VinculoIdVazio_LancaDomainException()
    {
        var act = () => Assinatura.Criar(Guid.Empty, PacoteId, TreinadorId, AlunoId, Valor);
        act.Should().Throw<DomainException>().WithMessage("O identificador do vínculo é inválido.");
    }

    [Fact]
    public void Criar_PacoteIdVazio_LancaDomainException()
    {
        var act = () => Assinatura.Criar(VinculoId, Guid.Empty, TreinadorId, AlunoId, Valor);
        act.Should().Throw<DomainException>().WithMessage("O identificador do pacote é inválido.");
    }

    [Fact]
    public void Criar_TreinadorIdVazio_LancaDomainException()
    {
        var act = () => Assinatura.Criar(VinculoId, PacoteId, Guid.Empty, AlunoId, Valor);
        act.Should().Throw<DomainException>().WithMessage("O identificador do treinador é inválido.");
    }

    [Fact]
    public void Criar_AlunoIdVazio_LancaDomainException()
    {
        var act = () => Assinatura.Criar(VinculoId, PacoteId, TreinadorId, Guid.Empty, Valor);
        act.Should().Throw<DomainException>().WithMessage("O identificador do aluno é inválido.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Criar_ValorInvalido_LancaDomainException(decimal valor)
    {
        var act = () => Assinatura.Criar(VinculoId, PacoteId, TreinadorId, AlunoId, valor);
        act.Should().Throw<DomainException>().WithMessage("O valor da assinatura deve ser maior que zero.");
    }

    // --- Ativar ---

    [Fact]
    public void Ativar_StatusPendente_MudaParaAtiva()
    {
        var a = CriarValida();
        a.Ativar();
        a.Status.Should().Be(AssinaturaStatus.Ativa);
        a.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Ativar_StatusCancelada_LancaDomainException()
    {
        var a = CriarValida();
        a.Cancelar();
        var act = () => a.Ativar();
        act.Should().Throw<DomainException>().WithMessage("Assinatura cancelada não pode ser ativada.");
    }

    // --- MarcarInadimplente ---

    [Fact]
    public void MarcarInadimplente_StatusAtiva_MudaParaInadimplente()
    {
        var a = CriarValida();
        a.Ativar();
        a.MarcarInadimplente();
        a.Status.Should().Be(AssinaturaStatus.Inadimplente);
        a.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarcarInadimplente_StatusPendente_LancaDomainException()
    {
        var a = CriarValida();
        var act = () => a.MarcarInadimplente();
        act.Should().Throw<DomainException>().WithMessage("Apenas assinaturas ativas podem ser marcadas como inadimplentes.");
    }

    // --- Cancelar ---

    [Fact]
    public void Cancelar_StatusAtiva_MudaParaCancelada()
    {
        var a = CriarValida();
        a.Ativar();
        a.Cancelar();
        a.Status.Should().Be(AssinaturaStatus.Cancelada);
        a.DataCancelamento.Should().NotBeNull();
    }

    [Fact]
    public void Cancelar_JaCancelada_LancaDomainException()
    {
        var a = CriarValida();
        a.Cancelar();
        var act = () => a.Cancelar();
        act.Should().Throw<DomainException>().WithMessage("A assinatura já está cancelada.");
    }

    // --- AgendarProximaCobranca ---

    [Fact]
    public void AgendarProximaCobranca_DataFutura_Atualiza()
    {
        var a = CriarValida();
        var futuro = DateTime.UtcNow.AddDays(30);

        a.AgendarProximaCobranca(futuro);

        a.DataProximaCobranca.Should().Be(futuro);
        a.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void AgendarProximaCobranca_DataPassada_LancaDomainException()
    {
        var a = CriarValida();
        var act = () => a.AgendarProximaCobranca(DateTime.UtcNow.AddDays(-1));
        act.Should().Throw<DomainException>().WithMessage("A data da próxima cobrança deve ser futura.");
    }
}
