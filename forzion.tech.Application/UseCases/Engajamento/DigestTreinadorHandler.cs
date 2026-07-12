using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Engajamento;

public class DigestTreinadorHandler(
    IExecucaoTreinoRepository execucaoTreinoRepository,
    INotificacaoRepository notificacaoRepository,
    IDigestTreinadorEmailNotifier digestEmailNotifier,
    TimeProvider timeProvider)
{
    public virtual async Task<int> HandleAsync(CancellationToken cancellationToken = default)
    {
        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var hoje = DateOnly.FromDateTime(agora);

        var snapshots = await execucaoTreinoRepository
            .ProjetarDigestTreinadoresAsync(hoje, cancellationToken)
            .ConfigureAwait(false);

        var gerados = 0;
        foreach (var snapshot in snapshots)
        {
            var (titulo, corpo) = Conteudo(snapshot.Treinaram, snapshot.NaoTreinaram);
            var notificacao = Notificacao.Criar(
                snapshot.TreinadorContaId, TipoNotificacao.DigestTreinador, titulo, corpo, agora, diaReferencia: hoje);
            if (notificacao.IsFailure) continue;

            var inserido = await notificacaoRepository.AdicionarAsync(notificacao.Value, cancellationToken).ConfigureAwait(false);
            gerados++;

            if (inserido)
                await digestEmailNotifier
                    .NotificarAsync(snapshot.TreinadorId, snapshot.Treinaram, snapshot.NaoTreinaram, cancellationToken)
                    .ConfigureAwait(false);
        }
        return gerados;
    }

    private static (string Titulo, string Corpo) Conteudo(int treinaram, int naoTreinaram) =>
        ("Resumo do dia",
         $"Hoje {treinaram} aluno(s) treinaram e {naoTreinaram} não treinaram. Confira a aderência da sua equipe.");
}
