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
        "AprovarTreinadorHandler",
        "AprovarVinculoHandler",
        "AtribuirPlanoHandler",
        "AtualizarExercicioHandler",
        "AtualizarGrupoMuscularHandler",
        "AtualizarHealthReportConfigHandler",
        "AtualizarPacoteHandler",
        "AtualizarPlanoPlataformaHandler",
        "CadastrarAlunoHandler",
        "CancelarAssinaturaAlunoHandler",
        "CancelarMinhaAssinaturaAlunoHandler",
        "CancelarNfseHandler",
        "CancelarMinhaAssinaturaTreinadorHandler",
        "CopiarExercicioGlobalHandler",
        "ObterDadosFiscaisTreinadorHandler",
        "ListarNotasFiscaisTreinadorHandler",
        "ObterDanfseTreinadorHandler",
        "ListarNotasFiscaisAdminHandler",
        "CriarAssinaturaAlunoHandler",
        "CriarExercicioHandler",
        "CriarGrupoMuscularHandler",
        "CriarPacoteHandler",
        "DespacharPreAvisosAlunoHandler",
        "DespacharPreAvisosTreinadorHandler",
        "ExcluirContaTesteHandler",
        "ExcluirPacoteHandler",
        "ExcluirTreinadorHandler",
        "ExecutarRelatorioSaudeHandler",
        "ExportarDadosPessoaisHandler",
        "GerarCobrancaMensalHandler",
        "GerarCobrancaPlanoTreinadorHandler",
        "GerarNfseComissaoMensalHandler",
        "InativarTreinadorHandler",
        "IniciarOnboardingTreinadorHandler",
        "IniciarPagamentoPlanoHandler",
        "ListarAlunosAdminHandler",
        "ListarContasElegivelPurgaLgpdHandler",
        "ListarContasTesteHandler",
        "ListarExerciciosHandler",
        "ListarFichasAlunoHandler",
        "ListarFichasDoAlunoHandler",
        "ListarGruposMuscularesHandler",
        "ListarHealthSnapshotsHandler",
        "ListarPacotesHandler",
        "ListarPagamentosAssinaturaAlunoHandler",
        "ListarPlanosPlataformaHandler",
        "ListarRecebimentosTreinadorHandler",
        "ListarTreinadoresHandler",
        "ListarTreinadoresPublicosHandler",
        "ListarTreinosDoTreinadorHandler",
        "ListarVinculosHandler",
        "LoginHandler",
        "ObterAdminDashboardHandler",
        "ObterAssinaturaAlunoHandler",
        "ObterDashboardStatsHandler",
        "ObterHealthReportConfigHandler",
        "ObterPreviewModoPagamentoTreinadorHandler",
        "ObterStatusPagamentoHandler",
        "ObterTreinadorHandler",
        "ObterVinculoAlunoHandler",
        "ProcessarWebhookStripeHandler",
        "ReativarVinculoHandler",
        "ReconciliarNfseHandler",
        "ReconciliarPagamentosStripeHandler",
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
