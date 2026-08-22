using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Application.UseCases.Admin.Leads;

public record BuscarLeadsPorContatoQuery(string Contato);

public record LeadAdminItem(
    Guid Id,
    Guid TreinadorId,
    string Nome,
    TipoContatoLead ContatoTipo,
    string ContatoValor,
    LeadStatus Status,
    bool Anonimizado,
    DateTime CreatedAt);

public class BuscarLeadsPorContatoHandler(ILeadRepository leadRepository)
{
    public virtual Task<IReadOnlyList<LeadAdminItem>> HandleAsync(
        BuscarLeadsPorContatoQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return HandleAsyncCore(query, cancellationToken);
    }

    private async Task<IReadOnlyList<LeadAdminItem>> HandleAsyncCore(
        BuscarLeadsPorContatoQuery query,
        CancellationToken cancellationToken)
    {
        // Mesma canonicalização usada na escrita (ContatoLead.Criar) — write=read, senão um
        // telefone gravado em E.164 nunca casaria com o formato que o operador digitou.
        var valorNormalizado = query.Contato.Contains('@')
            ? NormalizarEmail(query.Contato)
            : PhoneNumberNormalizer.Normalizar(query.Contato);

        if (valorNormalizado is null)
            return [];

        var leads = await leadRepository.BuscarPorContatoCrossTenantAsync(valorNormalizado, cancellationToken).ConfigureAwait(false);

        return leads
            .Select(l => new LeadAdminItem(l.Id, l.TreinadorId, l.Nome, l.Contato.Tipo, l.Contato.Valor, l.Status, l.Anonimizado, l.CreatedAt))
            .ToList();
    }

    private static string? NormalizarEmail(string valor)
    {
        var resultado = Email.Criar(valor);
        return resultado.IsSuccess ? resultado.Value.Value : null;
    }
}
