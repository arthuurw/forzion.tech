using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Leads;
using forzion.tech.Domain.Entities;

namespace forzion.tech.Application.UseCases.Alunos.RegistrarAluno;

public sealed record LeadConviteResolvido(LeadConvite Convite, Lead Lead);

/// <summary>
/// Isola o ramo de convite do cadastro de aluno: resolve o token no início (token
/// inválido/expirado/consumido devolve null, sem erro — cadastro normal segue) e consome
/// convite+lead no fim, dentro da MESMA transação do <see cref="RegistrarAlunoHandler"/>.
/// </summary>
public sealed class LeadConviteResolver(
    ILeadConviteRepository leadConviteRepository,
    ILeadRepository leadRepository)
{
    public async Task<LeadConviteResolvido?> ResolverAsync(string? tokenCru, DateTime agora, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tokenCru))
            return null;

        var hash = LeadConviteToken.Hash(tokenCru);
        var convite = await leadConviteRepository.ObterPorTokenHashAsync(hash, cancellationToken).ConfigureAwait(false);
        if (convite is null || convite.UsadoEm is not null || convite.InvalidadoEm is not null || convite.ExpiraEm <= agora)
            return null;

        var lead = await leadRepository
            .ObterComHistoricoAsync(convite.TreinadorId, convite.LeadId, cancellationToken)
            .ConfigureAwait(false);
        if (lead is null)
            return null;

        return new LeadConviteResolvido(convite, lead);
    }
}
