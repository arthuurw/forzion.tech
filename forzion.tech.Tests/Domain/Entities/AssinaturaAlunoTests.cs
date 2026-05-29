using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Domain.Entities;

public class AssinaturaAlunoTests
{
    private static readonly Guid VinculoId = Guid.NewGuid();
    private static readonly Guid PacoteId = Guid.NewGuid();
    private static readonly Guid TreinadorId = Guid.NewGuid();
    private static readonly Guid AlunoId = Guid.NewGuid();
    private const decimal Valor = 150m;

    private static AssinaturaAluno CriarValida() =>
        new AssinaturaAlunoBuilder()
            .ComVinculoId(VinculoId)
            .ComPacoteId(PacoteId)
            .ComTreinadorId(TreinadorId)
            .ComAlunoId(AlunoId)
            .ComValor(Valor)
            .Build();

    // --- Criar ---

    [Fact]
    public void Criar_DadosValidos_RetornaAssinaturaAlunoComStatusPendente()
    {
        var a = CriarValida();

        a.Id.Should().NotBeEmpty();
        a.VinculoId.Should().Be(VinculoId);
        a.PacoteId.Should().Be(PacoteId);
        a.TreinadorId.Should().Be(TreinadorId);
        a.AlunoId.Should().Be(AlunoId);
        a.Valor.Should().Be(Valor);
        a.Status.Should().Be(AssinaturaAlunoStatus.Pendente);
        a.DataCancelamento.Should().BeNull();
    }

    [Fact]
    public void Criar_DadosValidos_DispararaAssinaturaAlunoCriadaEvent()
    {
        var a = CriarValida();

        a.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<AssinaturaAlunoCriadaEvent>();
    }

    [Fact]
    public void Criar_VinculoIdVazio_LancaDomainException()
    {
        var act = () => AssinaturaAluno.Criar(Guid.Empty, PacoteId, TreinadorId, AlunoId, Valor, TestData.Agora);
        act.Should().Throw<DomainException>().WithMessage("O identificador do vínculo é inválido.");
    }

    [Fact]
    public void Criar_PacoteIdVazio_LancaDomainException()
    {
        var act = () => AssinaturaAluno.Criar(VinculoId, Guid.Empty, TreinadorId, AlunoId, Valor, TestData.Agora);
        act.Should().Throw<DomainException>().WithMessage("O identificador do pacote é inválido.");
    }

    [Fact]
    public void Criar_TreinadorIdVazio_LancaDomainException()
    {
        var act = () => AssinaturaAluno.Criar(VinculoId, PacoteId, Guid.Empty, AlunoId, Valor, TestData.Agora);
        act.Should().Throw<DomainException>().WithMessage("O identificador do treinador é inválido.");
    }

    [Fact]
    public void Criar_AlunoIdVazio_LancaDomainException()
    {
        var act = () => AssinaturaAluno.Criar(VinculoId, PacoteId, TreinadorId, Guid.Empty, Valor, TestData.Agora);
        act.Should().Throw<DomainException>().WithMessage("O identificador do aluno é inválido.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Criar_ValorInvalido_LancaDomainException(decimal valor)
    {
        var act = () => AssinaturaAluno.Criar(VinculoId, PacoteId, TreinadorId, AlunoId, valor, TestData.Agora);
        act.Should().Throw<DomainException>().WithMessage("O valor da assinatura deve ser maior que zero.");
    }

    // --- Ativar ---

    [Fact]
    public void Ativar_StatusPendente_MudaParaAtiva()
    {
        var a = CriarValida();
        a.Ativar();
        a.Status.Should().Be(AssinaturaAlunoStatus.Ativa);
        a.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Ativar_StatusCancelada_LancaDomainException()
    {
        var a = CriarValida();
        a.Cancelar();
        var act = () => a.Ativar();
        act.Should().Throw<DomainException>().WithMessage("AssinaturaAluno cancelada não pode ser ativada.");
    }

    // --- MarcarInadimplente ---

    [Fact]
    public void MarcarInadimplente_StatusAtiva_MudaParaInadimplente()
    {
        var a = CriarValida();
        a.Ativar();
        a.MarcarInadimplente();
        a.Status.Should().Be(AssinaturaAlunoStatus.Inadimplente);
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
        a.Status.Should().Be(AssinaturaAlunoStatus.Cancelada);
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
        var futuro = TestData.Agora.AddDays(30);

        a.AgendarProximaCobranca(futuro, TestData.Agora);

        a.DataProximaCobranca.Should().Be(futuro);
        a.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void AgendarProximaCobranca_DataPassada_LancaDomainException()
    {
        var a = CriarValida();
        var act = () => a.AgendarProximaCobranca(TestData.Agora.AddDays(-1), TestData.Agora);
        act.Should().Throw<DomainException>().WithMessage("A data da próxima cobrança deve ser futura.");
    }

    // --- RegistrarPagamentoFalho (IH.1) ---

    [Fact]
    public void RegistrarPagamentoFalho_PrimeiraTentativa_IncrementaContadorEDispatchEvento()
    {
        var a = CriarValida();
        a.Ativar();
        a.ClearDomainEvents();

        a.RegistrarPagamentoFalho(TestData.Agora);

        a.TentativasFalhasConsecutivas.Should().Be(1);
        a.Status.Should().Be(AssinaturaAlunoStatus.Ativa, "1 tentativa < threshold 3");
        a.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PagamentoFalhouEvent>()
            .Which.TentativasFalhasConsecutivas.Should().Be(1);
    }

    [Fact]
    public void RegistrarPagamentoFalho_TerceiraTentativaConsecutiva_MarcaInadimplenteEDispatcha2Eventos()
    {
        var a = CriarValida();
        a.Ativar();

        a.RegistrarPagamentoFalho(TestData.Agora);
        a.RegistrarPagamentoFalho(TestData.Agora);
        a.ClearDomainEvents();
        a.RegistrarPagamentoFalho(TestData.Agora);

        a.TentativasFalhasConsecutivas.Should().Be(3);
        a.Status.Should().Be(AssinaturaAlunoStatus.Inadimplente);
        a.DomainEvents.Should().HaveCount(2);
        a.DomainEvents.OfType<PagamentoFalhouEvent>().Should().ContainSingle();
        a.DomainEvents.OfType<AssinaturaAlunoMarcadaInadimplenteEvent>().Should().ContainSingle()
            .Which.TentativasFalhasConsecutivas.Should().Be(3);
    }

    [Fact]
    public void RegistrarPagamentoFalho_QuartaTentativaJaInadimplente_NaoRedispatchInadimplenteEvento()
    {
        var a = CriarValida();
        a.Ativar();
        a.RegistrarPagamentoFalho(TestData.Agora);
        a.RegistrarPagamentoFalho(TestData.Agora);
        a.RegistrarPagamentoFalho(TestData.Agora); // marca inadimplente
        a.ClearDomainEvents();

        a.RegistrarPagamentoFalho(TestData.Agora); // 4ª tentativa

        a.TentativasFalhasConsecutivas.Should().Be(4);
        a.Status.Should().Be(AssinaturaAlunoStatus.Inadimplente);
        a.DomainEvents.OfType<PagamentoFalhouEvent>().Should().ContainSingle();
        a.DomainEvents.OfType<AssinaturaAlunoMarcadaInadimplenteEvent>().Should().BeEmpty(
            "evento Inadimplente já foi disparado na transição — não re-emitir");
    }

    [Fact]
    public void RegistrarPagamentoFalho_AssinaturaCancelada_NoOp()
    {
        var a = CriarValida();
        a.Ativar();
        a.Cancelar();
        a.ClearDomainEvents();

        a.RegistrarPagamentoFalho(TestData.Agora);

        a.TentativasFalhasConsecutivas.Should().Be(0);
        a.Status.Should().Be(AssinaturaAlunoStatus.Cancelada);
        a.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void RegistrarPagamentoFalho_AssinaturaPendente_ContaSemTransicionar()
    {
        var a = CriarValida();
        // status = Pendente (sem Ativar)

        a.RegistrarPagamentoFalho(TestData.Agora);
        a.RegistrarPagamentoFalho(TestData.Agora);
        a.RegistrarPagamentoFalho(TestData.Agora);

        a.TentativasFalhasConsecutivas.Should().Be(3);
        a.Status.Should().Be(AssinaturaAlunoStatus.Pendente, "só Ativa transiciona pra Inadimplente");
        a.DomainEvents.OfType<AssinaturaAlunoMarcadaInadimplenteEvent>().Should().BeEmpty();
    }

    // --- RegistrarPagamentoRegularizado (IH.1) ---

    [Fact]
    public void RegistrarPagamentoRegularizado_ZeraContadorEReativaSeInadimplente()
    {
        var a = CriarValida();
        a.Ativar();
        a.RegistrarPagamentoFalho(TestData.Agora);
        a.RegistrarPagamentoFalho(TestData.Agora);
        a.RegistrarPagamentoFalho(TestData.Agora);
        a.Status.Should().Be(AssinaturaAlunoStatus.Inadimplente);

        a.RegistrarPagamentoRegularizado(TestData.Agora);

        a.TentativasFalhasConsecutivas.Should().Be(0);
        a.Status.Should().Be(AssinaturaAlunoStatus.Ativa);
    }

    [Fact]
    public void RegistrarPagamentoRegularizado_AssinaturaAtivaComFalhasParciais_SoZeraContador()
    {
        var a = CriarValida();
        a.Ativar();
        a.RegistrarPagamentoFalho(TestData.Agora);
        a.RegistrarPagamentoFalho(TestData.Agora);
        a.TentativasFalhasConsecutivas.Should().Be(2);
        a.Status.Should().Be(AssinaturaAlunoStatus.Ativa);

        a.RegistrarPagamentoRegularizado(TestData.Agora);

        a.TentativasFalhasConsecutivas.Should().Be(0);
        a.Status.Should().Be(AssinaturaAlunoStatus.Ativa);
    }

    [Fact]
    public void RegistrarPagamentoRegularizado_AssinaturaCancelada_NoOp()
    {
        var a = CriarValida();
        a.Ativar();
        a.RegistrarPagamentoFalho(TestData.Agora);
        a.Cancelar();

        a.RegistrarPagamentoRegularizado(TestData.Agora);

        a.TentativasFalhasConsecutivas.Should().Be(1, "cancelada não permite mexer no estado");
        a.Status.Should().Be(AssinaturaAlunoStatus.Cancelada);
    }

    [Fact]
    public void RegistrarPagamentoRegularizado_Idempotente_ChamadasMultiplasSemDano()
    {
        var a = CriarValida();
        a.Ativar();

        a.RegistrarPagamentoRegularizado(TestData.Agora);
        a.RegistrarPagamentoRegularizado(TestData.Agora);

        a.TentativasFalhasConsecutivas.Should().Be(0);
        a.Status.Should().Be(AssinaturaAlunoStatus.Ativa);
    }
}
