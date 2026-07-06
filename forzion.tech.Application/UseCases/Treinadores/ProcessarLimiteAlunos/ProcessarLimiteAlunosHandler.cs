using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinadores.ProcessarLimiteAlunos;

// Autoridade única: carimba o início da graça, envia lembretes, e apara o excedente no fim
// da janela de 3 meses. Recomputa tudo ao vivo a cada execução — idempotente por design (uma
// regularização entre execuções zera o excedente naturalmente).
public class ProcessarLimiteAlunosHandler(
    ITreinadorRepository treinadorRepository,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<ProcessarLimiteAlunosHandler> logger)
{
    private const int TamanhoLote = 100;
    private const string LinkPortal = "/treinador/plano";

    public virtual async Task<ProcessarLimiteAlunosResultado> HandleAsync(CancellationToken cancellationToken = default)
    {
        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var carimbados = 0;
        var lembretes = 0;
        var aparados = 0;
        Guid? aposId = null;

        while (true)
        {
            var lote = await treinadorRepository
                .ListarAtivosKeysetAsync(aposId, TamanhoLote, cancellationToken).ConfigureAwait(false);
            if (lote.Count == 0) break;

            foreach (var treinadorId in lote.Select(t => t.Id))
            {
                using var scope = scopeFactory.CreateScope();
                var (carimbou, enviouLembrete, aparou) = await ProcessarTreinadorAsync(
                    scope.ServiceProvider, treinadorId, agora, cancellationToken).ConfigureAwait(false);

                if (carimbou) carimbados++;
                if (enviouLembrete) lembretes++;
                if (aparou) aparados++;
            }

            aposId = lote[^1].Id;
            if (lote.Count < TamanhoLote) break;
        }

        return new ProcessarLimiteAlunosResultado(carimbados, lembretes, aparados);
    }

    private async Task<(bool Carimbou, bool EnviouLembrete, bool Aparou)> ProcessarTreinadorAsync(
        IServiceProvider services, Guid treinadorId, DateTime agora, CancellationToken cancellationToken)
    {
        var treinadorRepo = services.GetRequiredService<ITreinadorRepository>();
        var planoEfetivoResolver = services.GetRequiredService<IPlanoEfetivoResolver>();
        var vinculoRepo = services.GetRequiredService<IVinculoTreinadorAlunoRepository>();
        var notificacaoRepo = services.GetRequiredService<INotificacaoRepository>();
        var emailSender = services.GetRequiredService<ILimiteAlunosEmailSender>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var dbErrorInspector = services.GetRequiredService<IDatabaseErrorInspector>();

        var treinador = await treinadorRepo.ObterPorIdAsync(treinadorId, cancellationToken).ConfigureAwait(false);
        if (treinador is null) return (false, false, false);

        var planoEfetivo = await planoEfetivoResolver.ResolverAsync(treinador, cancellationToken).ConfigureAwait(false);
        var ativos = await vinculoRepo.ContarAtivosPorTreinadorAsync(treinadorId, cancellationToken).ConfigureAwait(false);
        var excedente = Math.Max(0, ativos - planoEfetivo.MaxAlunos);

        if (excedente == 0)
        {
            if (treinador.AlunosAcimaDoCapDesde is null) return (false, false, false);

            treinador.LimparAcimaDoCap(agora);
            await CommitOuIgnorarConcorrenciaAsync(unitOfWork, dbErrorInspector, treinadorId, cancellationToken).ConfigureAwait(false);
            return (false, false, false);
        }

        if (treinador.AlunosAcimaDoCapDesde is null)
        {
            treinador.MarcarAcimaDoCap(agora);
            if (!await CommitOuIgnorarConcorrenciaAsync(unitOfWork, dbErrorInspector, treinadorId, cancellationToken).ConfigureAwait(false))
                return (false, false, false);

            var dataLimiteInicio = agora.AddMonths(3);
            await EnviarNotificacaoAsync(
                notificacaoRepo, treinador, TipoNotificacao.LimiteAlunosExcedido,
                "Limite de alunos excedido",
                $"Você está com {excedente} aluno(s) acima do limite do seu plano. Regularize até {dataLimiteInicio:dd/MM/yyyy}.",
                agora,
                ct => emailSender.EnviarInicioAsync(treinador.ContaId, treinador.Nome, excedente, dataLimiteInicio, ct),
                cancellationToken).ConfigureAwait(false);

            return (true, false, false);
        }

        var dataLimiteAtual = treinador.AlunosAcimaDoCapDesde.Value.AddMonths(3);

        if (agora < dataLimiteAtual)
        {
            var diasParaLimite = (dataLimiteAtual.Date - agora.Date).Days;
            if (diasParaLimite is not (30 or 7 or 1))
                return (false, false, false);

            var enviouLembrete = await EnviarNotificacaoAsync(
                notificacaoRepo, treinador, TipoNotificacao.LimiteAlunosLembrete,
                "Lembrete — regularize o limite de alunos",
                $"Você continua com {excedente} aluno(s) acima do limite do seu plano. Prazo: {dataLimiteAtual:dd/MM/yyyy}.",
                agora,
                ct => emailSender.EnviarLembreteAsync(treinador.ContaId, treinador.Nome, excedente, dataLimiteAtual, ct),
                cancellationToken).ConfigureAwait(false);

            return (false, enviouLembrete, false);
        }

        // Deadline atingido: recomputa AO VIVO (não reusa "ativos"/"excedente" do topo do
        // método) — uma regularização concorrente pode ter commitado nos awaits acima, e a
        // apara precisa enxergar o estado atual, não o de alguns awaits atrás.
        var ativosAgora = await vinculoRepo.ContarAtivosPorTreinadorAsync(treinadorId, cancellationToken).ConfigureAwait(false);
        var planoAgora = await planoEfetivoResolver.ResolverAsync(treinador, cancellationToken).ConfigureAwait(false);
        var excedenteAgora = Math.Max(0, ativosAgora - planoAgora.MaxAlunos);

        if (excedenteAgora == 0)
        {
            treinador.LimparAcimaDoCap(agora);
            await CommitOuIgnorarConcorrenciaAsync(unitOfWork, dbErrorInspector, treinadorId, cancellationToken).ConfigureAwait(false);
            return (false, false, false);
        }

        var ordenados = await vinculoRepo.ListarAtivosPorTreinadorOrdenadoAsync(treinadorId, cancellationToken).ConfigureAwait(false);
        var desativar = CalcularExcedentes(ordenados, planoAgora.MaxAlunos);

        foreach (var vinculo in desativar)
        {
            var inativarResult = vinculo.Inativar(agora);
            if (inativarResult.IsFailure)
                logger.LogWarning(
                    "ProcessarLimiteAlunos: falha ao inativar vinculo {VinculoId} do treinador {TreinadorId}: {Erro}.",
                    vinculo.Id, treinadorId, inativarResult.Error!.Message);
        }

        treinador.LimparAcimaDoCap(agora);
        if (!await CommitOuIgnorarConcorrenciaAsync(unitOfWork, dbErrorInspector, treinadorId, cancellationToken).ConfigureAwait(false))
            return (false, false, false);

        if (desativar.Count == 0) return (false, false, false);

        await EnviarNotificacaoAsync(
            notificacaoRepo, treinador, TipoNotificacao.LimiteAlunosAplicado,
            "Ajuste aplicado no limite de alunos",
            $"{desativar.Count} vínculo(s) foram desativados automaticamente por excesso de capacidade.",
            agora,
            ct => emailSender.EnviarAplicadoAsync(treinador.ContaId, treinador.Nome, desativar.Count, ct),
            cancellationToken).ConfigureAwait(false);

        // Log só com contagem/ids — nunca nome/telefone/e-mail de aluno (LGPD, design.md §5).
        logger.LogInformation(
            "ProcessarLimiteAlunos: {Count} vinculo(s) desativados por excesso de capacidade para o treinador {TreinadorId}.",
            desativar.Count, treinadorId);

        return (false, false, true);
    }

    private async Task<bool> CommitOuIgnorarConcorrenciaAsync(
        IUnitOfWork unitOfWork, IDatabaseErrorInspector dbErrorInspector, Guid treinadorId, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (dbErrorInspector.EhConflitoDeConcorrenciaOtimista(ex))
        {
            unitOfWork.DescartarAlteracoesPendentes();
            logger.LogDebug(ex,
                "ProcessarLimiteAlunos: conflito de concorrência otimista no treinador {TreinadorId}; próxima execução reprocessa.",
                treinadorId);
            return false;
        }
    }

    private static async Task<bool> EnviarNotificacaoAsync(
        INotificacaoRepository notificacaoRepo,
        Treinador treinador,
        TipoNotificacao tipo,
        string titulo,
        string corpo,
        DateTime agora,
        Func<CancellationToken, Task> enviarEmail,
        CancellationToken cancellationToken)
    {
        var notificacaoResult = Notificacao.Criar(
            treinador.ContaId, tipo, titulo, corpo, agora,
            linkRelativo: LinkPortal, diaReferencia: DateOnly.FromDateTime(agora));
        if (notificacaoResult.IsFailure) return false;

        var inserido = await notificacaoRepo.AdicionarAsync(notificacaoResult.Value, cancellationToken).ConfigureAwait(false);
        if (inserido)
            await enviarEmail(cancellationToken).ConfigureAwait(false);

        return inserido;
    }

    private static IReadOnlyList<VinculoTreinadorAluno> CalcularExcedentes(
        IReadOnlyList<VinculoTreinadorAluno> ordenadosPorAntiguidade, int capEfetivo)
    {
        var preservados = ordenadosPorAntiguidade.Where(v => v.PreservarNoLimite).ToList();
        var naoPreservados = ordenadosPorAntiguidade.Where(v => !v.PreservarNoLimite).ToList();

        if (preservados.Count > capEfetivo)
            return preservados.Skip(capEfetivo).Concat(naoPreservados).ToList();

        var vagas = capEfetivo - preservados.Count;
        return naoPreservados.Skip(vagas).ToList();
    }
}
