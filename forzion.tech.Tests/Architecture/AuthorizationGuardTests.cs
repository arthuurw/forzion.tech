using System.Reflection;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Services;
using NetArchTest.Rules;

namespace forzion.tech.Tests.Architecture;

public class AuthorizationGuardTests
{
    private static readonly Assembly ApplicationAssembly = typeof(LimiteTreinadorService).Assembly;

    private static readonly IReadOnlySet<string> SemUserContextPermitidos = new HashSet<string>
    {
        "AlterarModoPagamentoTreinadorHandler",
        "AnonimizarLeadHandler",
        "AprovarTreinadorHandler",
        "AprovarVinculoHandler",
        "AtualizarExercicioHandler",
        "AtualizarGrupoMuscularHandler",
        "AtualizarHealthReportConfigHandler",
        "AtualizarPacoteHandler",
        "AtualizarPlanoPlataformaHandler",
        "AtualizarStatusLeadHandler",
        "BuscarLeadsPorContatoHandler",
        "CadastrarAlunoHandler",
        "CriarLeadManualHandler",
        "CancelarAssinaturaAlunoHandler",
        "CancelarMinhaAssinaturaAlunoHandler",
        "CancelarMinhaAssinaturaTreinadorHandler",
        "ConsultarDisponibilidadeAgenteHandler",
        "ContratarPlanoTreinadorHandler",
        "CopiarExercicioGlobalHandler",
        "ObterDadosFiscaisTreinadorHandler",
        "CriarAssinaturaAlunoHandler",
        "CriarBloqueioAgendaHandler",
        "CriarExercicioHandler",
        "CriarGrupoMuscularHandler",
        "CriarPacoteHandler",
        "DefinirCortesiaHandler",
        "DefinirPerfilPublicoTreinadorHandler",
        "DefinirPreservacaoVinculoHandler",
        "DespacharPreAvisosAlunoHandler",
        "DespacharPreAvisosTreinadorHandler",
        "DigestTreinadorHandler",
        "EnviarConviteLeadHandler",
        "ExcluirContaTesteHandler",
        "ExcluirPacoteHandler",
        "ExcluirTreinadorHandler",
        "ExecutarRelatorioSaudeHandler",
        "ExportarDadosPessoaisHandler",
        "GerarCobrancaMensalHandler",
        "GerarCobrancaPlanoTreinadorHandler",
        "InativarTreinadorHandler",
        "IniciarOnboardingTreinadorHandler",
        "IniciarPagamentoPlanoHandler",
        "ListarAlunosAdminHandler",
        "ListarContasElegivelPurgaLgpdHandler",
        "ListarContasTesteHandler",
        "ListarExerciciosHandler",
        "ListarBloqueiosAgendaHandler",
        "ListarFichasAlunoHandler",
        "ListarFichasDoAlunoHandler",
        "ListarGruposMuscularesHandler",
        "ListarHealthSnapshotsHandler",
        "ListarLeadsHandler",
        "ListarPacotesHandler",
        "ListarPagamentosAssinaturaAlunoHandler",
        "ListarPlanosPlataformaHandler",
        "ListarRecebimentosTreinadorHandler",
        "ListarServicosHandler",
        "ListarTreinadoresHandler",
        "ListarTreinadoresPublicosHandler",
        "ListarTreinosDoTreinadorHandler",
        "ListarVinculosHandler",
        "LoginHandler",
        "NudgeAderenciaHandler",
        "ObterAdminDashboardHandler",
        "ObterAssinaturaAlunoHandler",
        "ObterBusinessInfoHandler",
        "ObterDashboardStatsHandler",
        "ObterHealthReportConfigHandler",
        "ObterLeadHandler",
        "ObterMetricasLeadsHandler",
        "ObterPerfilPublicoTreinadorHandler",
        "ObterPreviewModoPagamentoTreinadorHandler",
        "ObterStatusPagamentoHandler",
        "ObterTreinadorHandler",
        "ObterVinculoAlunoHandler",
        "ProcessarLimiteAlunosHandler",
        "ProcessarWebhookStripeHandler",
        "ReativarVinculoHandler",
        "ReconciliarPagamentosStripeHandler",
        "RemoverBloqueioAgendaHandler",
        "RegistrarInteracaoLeadHandler",
        "ResolverConviteLeadHandler",
        "RegistrarLeadAgenteHandler",
        "ConfirmarTrocaEmailHandler",
        "RedefinirSenhaHandler",
        "RegistrarAlunoHandler",
        "RegistrarTreinadorHandler",
        "RenovarSessaoHandler",
        "ReprovarTreinadorHandler",
        "TrocarPlanoTreinadorHandler",
        "VerificarEmailHandler",
        "VerificarOnboardingTreinadorHandler",
    };

    private static IEnumerable<Type> Handlers() =>
        Types.InAssembly(ApplicationAssembly)
            .That()
            .HaveNameEndingWith("Handler")
            .And()
            .AreClasses()
            .GetTypes()
            .Where(t => !t.IsAbstract);

    private static bool InjetaUserContext(Type handler) =>
        handler.GetConstructors()
            .Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(IUserContext)));

    [Fact]
    public void HandlerSemUserContext_DeveEstarNaAllowlistDeNaoEscopados()
    {
        var semUserContext = Handlers()
            .Where(t => !InjetaUserContext(t))
            .Select(t => t.Name)
            .ToHashSet();

        var inesperados = semUserContext.Except(SemUserContextPermitidos).OrderBy(n => n).ToList();
        var obsoletos = SemUserContextPermitidos.Except(semUserContext).OrderBy(n => n).ToList();

        Assert.True(inesperados.Count == 0,
            "Handler novo sem IUserContext. Se precisa da identidade do chamador via token, injete IUserContext; "
            + $"senão adicione à allowlist (decisão consciente de authz): {string.Join(", ", inesperados)}");
        Assert.True(obsoletos.Count == 0,
            $"Allowlist desatualizada (handler agora injeta IUserContext ou não existe mais): {string.Join(", ", obsoletos)}");
    }
}
