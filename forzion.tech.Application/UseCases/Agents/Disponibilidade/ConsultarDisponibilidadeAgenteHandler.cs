using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Services;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Application.UseCases.Agents.Disponibilidade;

public class ConsultarDisponibilidadeAgenteHandler(
    ITreinadorRepository treinadorRepository,
    IPacoteRepository pacoteRepository,
    IBloqueioAgendaRepository bloqueioAgendaRepository,
    ISolicitacaoAgendamentoRepository solicitacaoAgendamentoRepository,
    TimeProvider timeProvider)
{
    public virtual Task<Result<IReadOnlyList<AvailabilitySlotResponse>>> HandleAsync(
        ConsultarDisponibilidadeQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return HandleAsyncCore(query, cancellationToken);
    }

    private async Task<Result<IReadOnlyList<AvailabilitySlotResponse>>> HandleAsyncCore(
        ConsultarDisponibilidadeQuery query,
        CancellationToken cancellationToken)
    {
        var treinador = await treinadorRepository.ObterPorIdAsync(query.TenantId, cancellationToken).ConfigureAwait(false);
        if (!AgentTenantGuard.EstaPublicado(treinador))
            return Result.Failure<IReadOnlyList<AvailabilitySlotResponse>>(TreinadorErrors.NaoEncontrado);

        var pacote = await pacoteRepository.ObterPorIdAsync(query.ServiceId, cancellationToken).ConfigureAwait(false);
        if (pacote is null || pacote.TreinadorId != query.TenantId || !pacote.IsPublico || pacote.DuracaoMinutos is not { } duracaoMinutos)
            return Result.Failure<IReadOnlyList<AvailabilitySlotResponse>>(PacoteErrors.NaoEncontrado);

        // Sem try/catch: se o fuso persistido não resolver (R7), a exceção sobe para o
        // AgentExceptionFilter e vira 503 — nunca 200 com lista vazia (AGF3-13.3).
        var fuso = TimeZoneInfo.FindSystemTimeZoneById(treinador.FusoHorario);
        var agora = timeProvider.GetUtcNow().UtcDateTime;

        var bloqueios = await bloqueioAgendaRepository
            .ListarVigentesAsync(query.TenantId, query.FromUtc, query.ToUtc, cancellationToken)
            .ConfigureAwait(false);

        var parametros = new ParametrosDerivacao(
            query.TenantId,
            query.ServiceId,
            duracaoMinutos,
            query.FromUtc,
            query.ToUtc,
            agora,
            fuso,
            treinador.PoliticaAgenda,
            treinador.PerfilPublico.HorariosFuncionamento,
            bloqueios);

        var slots = DerivadorDisponibilidade.Derivar(parametros);
        var capacidadeMaxima = pacote.CapacidadeMaxima;
        var serviceId = query.ServiceId.ToString();

        // Uma única consulta para o intervalo inteiro (AGF4-28); o abatimento por slot é feito
        // aqui em memória, por SOBREPOSIÇÃO de intervalo — nunca por igualdade de slotId (R2),
        // que quebraria em silêncio se o treinador mudasse a duração do pacote.
        var confirmadas = await solicitacaoAgendamentoRepository
            .ListarConfirmadasNoIntervaloAsync(query.TenantId, query.ServiceId, query.FromUtc, query.ToUtc, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success<IReadOnlyList<AvailabilitySlotResponse>>(
            [.. slots
                .Select(s => new
                {
                    Slot = s,
                    CapacityRemaining = Math.Max(0, capacidadeMaxima - confirmadas.Count(c => c.InicioUtc < s.FimUtc && c.FimUtc > s.InicioUtc))
                })
                .Where(x => x.CapacityRemaining > 0)
                .Select(x => new AvailabilitySlotResponse(
                    x.Slot.SlotId,
                    serviceId,
                    new DateTimeOffset(x.Slot.InicioUtc, TimeSpan.Zero),
                    new DateTimeOffset(x.Slot.FimUtc, TimeSpan.Zero),
                    x.CapacityRemaining))]);
    }
}
