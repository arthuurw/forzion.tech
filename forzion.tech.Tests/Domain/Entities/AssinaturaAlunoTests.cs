using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Events;
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
        var r = AssinaturaAluno.Criar(Guid.Empty, PacoteId, TreinadorId, AlunoId, Valor, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O identificador do vínculo é inválido.");
    }

    [Fact]
    public void Criar_PacoteIdVazio_LancaDomainException()
    {
        var r = AssinaturaAluno.Criar(VinculoId, Guid.Empty, TreinadorId, AlunoId, Valor, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O identificador do pacote é inválido.");
    }

    [Fact]
    public void Criar_TreinadorIdVazio_LancaDomainException()
    {
        var r = AssinaturaAluno.Criar(VinculoId, PacoteId, Guid.Empty, AlunoId, Valor, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O identificador do treinador é inválido.");
    }

    [Fact]
    public void Criar_AlunoIdVazio_LancaDomainException()
    {
        var r = AssinaturaAluno.Criar(VinculoId, PacoteId, TreinadorId, Guid.Empty, Valor, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O identificador do aluno é inválido.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Criar_ValorInvalido_LancaDomainException(decimal valor)
    {
        var r = AssinaturaAluno.Criar(VinculoId, PacoteId, TreinadorId, AlunoId, valor, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O valor da assinatura deve ser maior que zero.");
    }

    // --- Ativar ---

    [Fact]
    public void Ativar_StatusPendente_MudaParaAtiva()
    {
        var a = CriarValida();
        a.Ativar(TestData.Agora);
        a.Status.Should().Be(AssinaturaAlunoStatus.Ativa);
        a.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Ativar_StatusCancelada_LancaDomainException()
    {
        var a = CriarValida();
        a.Cancelar(TestData.Agora);
        var r = a.Ativar(TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("A assinatura cancelada não pode ser ativada.");
    }

    [Fact]
    public void Ativar_StatusInadimplente_RetornaFalhaExigeRegularizacao()
    {
        // G-PAY-4: Inadimplente → Ativa via Ativar é proibido; contador não seria zerado.
        var a = CriarValida();
        a.Ativar(TestData.Agora);
        a.RegistrarPagamentoFalho(TestData.Agora);
        a.RegistrarPagamentoFalho(TestData.Agora);
        a.RegistrarPagamentoFalho(TestData.Agora);
        a.Status.Should().Be(AssinaturaAlunoStatus.Inadimplente);
        a.TentativasFalhasConsecutivas.Should().Be(3);

        var r = a.Ativar(TestData.Agora);

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be("assinatura_aluno.inadimplente_deve_usar_regularizacao");
        a.Status.Should().Be(AssinaturaAlunoStatus.Inadimplente, "status não deve ter sido alterado");
        a.TentativasFalhasConsecutivas.Should().Be(3, "contador não deve ter sido zerado");
    }

    // --- MarcarInadimplente ---

    [Fact]
    public void MarcarInadimplente_StatusAtiva_MudaParaInadimplente()
    {
        var a = CriarValida();
        a.Ativar(TestData.Agora);
        a.MarcarInadimplente(TestData.Agora);
        a.Status.Should().Be(AssinaturaAlunoStatus.Inadimplente);
        a.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarcarInadimplente_StatusPendente_LancaDomainException()
    {
        var a = CriarValida();
        var r = a.MarcarInadimplente(TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("Apenas assinaturas ativas podem ser marcadas como inadimplentes.");
    }

    // --- Cancelar ---

    [Fact]
    public void Cancelar_StatusAtiva_MudaParaCancelada()
    {
        var a = CriarValida();
        a.Ativar(TestData.Agora);
        a.Cancelar(TestData.Agora);
        a.Status.Should().Be(AssinaturaAlunoStatus.Cancelada);
        a.DataCancelamento.Should().NotBeNull();
    }

    [Fact]
    public void Cancelar_JaCancelada_LancaDomainException()
    {
        var a = CriarValida();
        a.Cancelar(TestData.Agora);
        var r = a.Cancelar(TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("A assinatura já está cancelada.");
    }

    [Fact]
    public void Cancelar_StatusAtiva_DispatchaAssinaturaAlunoCanceladaEvent()
    {
        var a = CriarValida();
        a.Ativar(TestData.Agora);
        a.ClearDomainEvents();

        a.Cancelar(TestData.Agora);

        a.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<AssinaturaAlunoCanceladaEvent>()
            .Which.OcorridoEm.Should().Be(TestData.Agora);
    }

    [Fact]
    public void Cancelar_UsaParametroAgoraNasDatas()
    {
        var a = CriarValida();
        a.Ativar(TestData.Agora);
        var quando = new DateTime(2026, 7, 15, 10, 30, 0, DateTimeKind.Utc);

        a.Cancelar(quando);

        a.DataCancelamento.Should().Be(quando);
        a.UpdatedAt.Should().Be(quando);
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
        var r = a.AgendarProximaCobranca(TestData.Agora.AddDays(-1), TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("A data da próxima cobrança deve ser futura.");
    }

    // --- RegistrarPagamentoFalho (IH.1) ---

    [Fact]
    public void RegistrarPagamentoFalho_PrimeiraTentativa_IncrementaContadorEDispatchEvento()
    {
        var a = CriarValida();
        a.Ativar(TestData.Agora);
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
        a.Ativar(TestData.Agora);

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
        a.Ativar(TestData.Agora);
        a.RegistrarPagamentoFalho(TestData.Agora);
        a.RegistrarPagamentoFalho(TestData.Agora);
        a.RegistrarPagamentoFalho(TestData.Agora);
        a.ClearDomainEvents();

        a.RegistrarPagamentoFalho(TestData.Agora);

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
        a.Ativar(TestData.Agora);
        a.Cancelar(TestData.Agora);
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
        a.Ativar(TestData.Agora);
        a.RegistrarPagamentoFalho(TestData.Agora);
        a.RegistrarPagamentoFalho(TestData.Agora);
        a.RegistrarPagamentoFalho(TestData.Agora);
        a.Status.Should().Be(AssinaturaAlunoStatus.Inadimplente);

        a.RegistrarPagamentoRegularizado(TestData.Agora);

        a.TentativasFalhasConsecutivas.Should().Be(0);
        a.Status.Should().Be(AssinaturaAlunoStatus.Ativa);
    }

    [Fact]
    public void RegistrarPagamentoRegularizado_Inadimplente_DispatchaReativadaEvent()
    {
        var a = CriarValida();
        a.Ativar(TestData.Agora);
        a.RegistrarPagamentoFalho(TestData.Agora);
        a.RegistrarPagamentoFalho(TestData.Agora);
        a.RegistrarPagamentoFalho(TestData.Agora);
        a.ClearDomainEvents();

        a.RegistrarPagamentoRegularizado(TestData.Agora);

        a.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<AssinaturaAlunoReativadaEvent>()
            .Which.OcorridoEm.Should().Be(TestData.Agora);
    }

    [Fact]
    public void RegistrarPagamentoRegularizado_JaAtiva_NaoDispatchaReativadaEvent()
    {
        // Idempotência: chamar Regularizado numa assinatura já Ativa não dispara evento.
        var a = CriarValida();
        a.Ativar(TestData.Agora);
        a.ClearDomainEvents();

        a.RegistrarPagamentoRegularizado(TestData.Agora);

        a.DomainEvents.OfType<AssinaturaAlunoReativadaEvent>().Should().BeEmpty(
            "assinatura já estava Ativa; sem transição de estado, não deve disparar evento");
    }

    [Fact]
    public void RegistrarPagamentoRegularizado_AssinaturaAtivaComFalhasParciais_SoZeraContador()
    {
        var a = CriarValida();
        a.Ativar(TestData.Agora);
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
        a.Ativar(TestData.Agora);
        a.RegistrarPagamentoFalho(TestData.Agora);
        a.Cancelar(TestData.Agora);

        a.RegistrarPagamentoRegularizado(TestData.Agora);

        a.TentativasFalhasConsecutivas.Should().Be(1, "cancelada não permite mexer no estado");
        a.Status.Should().Be(AssinaturaAlunoStatus.Cancelada);
    }

    [Fact]
    public void RegistrarPagamentoRegularizado_Idempotente_ChamadasMultiplasSemDano()
    {
        var a = CriarValida();
        a.Ativar(TestData.Agora);

        a.RegistrarPagamentoRegularizado(TestData.Agora);
        a.RegistrarPagamentoRegularizado(TestData.Agora);

        a.TentativasFalhasConsecutivas.Should().Be(0);
        a.Status.Should().Be(AssinaturaAlunoStatus.Ativa);
    }

    // --- MarcarInadimplentePorDisputa (chargeback) ---

    [Fact]
    public void MarcarInadimplentePorDisputa_AssinaturaAtiva_ForcaTransicaoEEquiparaContadorAoLimite()
    {
        var a = CriarValida();
        a.Ativar(TestData.Agora);
        a.ClearDomainEvents();

        a.MarcarInadimplentePorDisputa(TestData.Agora);

        a.Status.Should().Be(AssinaturaAlunoStatus.Inadimplente);
        // Contador equiparado ao limite — sinaliza pra downstream que cruzou threshold.
        a.TentativasFalhasConsecutivas.Should().Be(AssinaturaAluno.LimiteTentativasFalhas);
        a.UpdatedAt.Should().NotBeNull();
        a.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<AssinaturaAlunoMarcadaInadimplenteEvent>()
            .Which.TentativasFalhasConsecutivas.Should().Be(AssinaturaAluno.LimiteTentativasFalhas);
    }

    [Fact]
    public void MarcarInadimplentePorDisputa_NaoDependeDeFalhasAcumuladas_TransicionaImediatamente()
    {
        // Diferença chave em relação a RegistrarPagamentoFalho: 1ª disputa já tranca.
        var a = CriarValida();
        a.Ativar(TestData.Agora);
        a.TentativasFalhasConsecutivas.Should().Be(0);

        a.MarcarInadimplentePorDisputa(TestData.Agora);

        a.Status.Should().Be(AssinaturaAlunoStatus.Inadimplente);
    }

    [Fact]
    public void MarcarInadimplentePorDisputa_AssinaturaCancelada_NoOp()
    {
        var a = CriarValida();
        a.Ativar(TestData.Agora);
        a.Cancelar(TestData.Agora);
        a.ClearDomainEvents();

        a.MarcarInadimplentePorDisputa(TestData.Agora);

        a.Status.Should().Be(AssinaturaAlunoStatus.Cancelada);
        a.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void MarcarInadimplentePorDisputa_JaInadimplente_NoOpIdempotente()
    {
        var a = CriarValida();
        a.Ativar(TestData.Agora);
        a.RegistrarPagamentoFalho(TestData.Agora);
        a.RegistrarPagamentoFalho(TestData.Agora);
        a.RegistrarPagamentoFalho(TestData.Agora);
        a.ClearDomainEvents();

        a.MarcarInadimplentePorDisputa(TestData.Agora);

        a.Status.Should().Be(AssinaturaAlunoStatus.Inadimplente);
        a.DomainEvents.Should().BeEmpty(
            "Inadimplente já está no estado correto; não re-dispara evento.");
    }

    [Fact]
    public void MarcarInadimplentePorDisputa_AssinaturaPendente_NoOp()
    {
        var a = CriarValida();

        a.MarcarInadimplentePorDisputa(TestData.Agora);

        a.Status.Should().Be(AssinaturaAlunoStatus.Pendente);
        a.TentativasFalhasConsecutivas.Should().Be(0);
        a.DomainEvents.OfType<AssinaturaAlunoMarcadaInadimplenteEvent>().Should().BeEmpty();
    }
}
