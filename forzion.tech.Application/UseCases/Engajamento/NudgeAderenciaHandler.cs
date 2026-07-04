using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Engajamento;

public class NudgeAderenciaHandler(
    IExecucaoTreinoRepository execucaoTreinoRepository,
    INotificacaoRepository notificacaoRepository,
    TimeProvider timeProvider)
{
    private static readonly int[] MarcosStreak = [7, 14, 30];

    public static IReadOnlyList<TipoNotificacao> ClassificarNudges(int diasSemTreino, int streak)
    {
        var nudges = new List<TipoNotificacao>(2);
        if (diasSemTreino <= 1)
        {
            nudges.Add(TipoNotificacao.Reforco);
            if (Array.IndexOf(MarcosStreak, streak) >= 0)
                nudges.Add(TipoNotificacao.MarcoStreak);
        }
        else if (diasSemTreino == 2)
        {
            nudges.Add(TipoNotificacao.LembreteLeve);
        }
        else
        {
            nudges.Add(TipoNotificacao.Recuperacao);
        }
        return nudges;
    }

    public virtual async Task<int> HandleAsync(CancellationToken cancellationToken = default)
    {
        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var hoje = DateOnly.FromDateTime(agora);

        var snapshots = await execucaoTreinoRepository
            .ProjetarAderenciaAtivosAsync(hoje, cancellationToken)
            .ConfigureAwait(false);

        var gerados = 0;
        foreach (var snapshot in snapshots)
        {
            var diasSemTreino = hoje.DayNumber - snapshot.UltimaExecucao.DayNumber;
            foreach (var tipo in ClassificarNudges(diasSemTreino, snapshot.Streak))
            {
                var (titulo, corpo) = Conteudo(tipo, snapshot.Streak);
                var notificacao = Notificacao.Criar(snapshot.ContaId, tipo, titulo, corpo, agora, diaReferencia: hoje);
                if (notificacao.IsFailure) continue;

                await notificacaoRepository.AdicionarAsync(notificacao.Value, cancellationToken).ConfigureAwait(false);
                gerados++;
            }
        }
        return gerados;
    }

    private static (string Titulo, string Corpo) Conteudo(TipoNotificacao tipo, int streak) => tipo switch
    {
        TipoNotificacao.Reforco => ("Mandou bem!", "Você treinou recentemente. Continue firme no ritmo!"),
        TipoNotificacao.LembreteLeve => ("Bora treinar?", "Faz 2 dias sem registrar um treino. Que tal manter o ritmo hoje?"),
        TipoNotificacao.Recuperacao => ("Vamos retomar", "Faz alguns dias sem treino. Bora voltar hoje e retomar o foco!"),
        TipoNotificacao.MarcoStreak => ($"Sequência de {streak} dias!", $"Você atingiu {streak} dias de treino consecutivos. Que consistência!"),
        _ => (string.Empty, string.Empty)
    };
}
