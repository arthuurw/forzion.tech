using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Application.UseCases.Leads.ResolverConviteLead;

public class ResolverConviteLeadHandler(
    ILeadConviteRepository leadConviteRepository,
    ILeadRepository leadRepository,
    ITreinadorRepository treinadorRepository,
    TimeProvider timeProvider)
{
    public virtual Task<Result<ResolverConviteLeadResponse>> HandleAsync(
        ResolverConviteLeadQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return HandleAsyncCore(query, cancellationToken);
    }

    private async Task<Result<ResolverConviteLeadResponse>> HandleAsyncCore(
        ResolverConviteLeadQuery query,
        CancellationToken cancellationToken)
    {
        var hash = LeadConviteToken.Hash(query.Token);
        var convite = await leadConviteRepository.ObterPorTokenHashAsync(hash, cancellationToken).ConfigureAwait(false);

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        // Os três casos (inexistente, expirado, consumido) colapsam no MESMO erro — divergir
        // confirmaria a quem tem o token qual estado o convite está, sem precisar de posse válida.
        if (convite is null || convite.UsadoEm is not null || convite.InvalidadoEm is not null || convite.ExpiraEm <= agora)
            return Result.Failure<ResolverConviteLeadResponse>(LeadConviteErrors.TokenInvalido);

        var lead = await leadRepository
            .ObterComHistoricoAsync(convite.TreinadorId, convite.LeadId, cancellationToken)
            .ConfigureAwait(false);
        if (lead is null || lead.Anonimizado)
            return Result.Failure<ResolverConviteLeadResponse>(LeadConviteErrors.TokenInvalido);

        var treinador = await treinadorRepository
            .ObterPorIdAsync(convite.TreinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (treinador is null)
            return Result.Failure<ResolverConviteLeadResponse>(LeadConviteErrors.TokenInvalido);

        return Result.Success(new ResolverConviteLeadResponse(
            lead.Nome, lead.Contato.Tipo, lead.Contato.Valor, convite.TreinadorId, treinador.Nome));
    }
}
