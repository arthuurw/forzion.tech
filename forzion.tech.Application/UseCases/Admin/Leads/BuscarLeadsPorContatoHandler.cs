using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Application.UseCases.Admin.Leads;

public record BuscarLeadsPorContatoQuery(string Contato, Guid AdminId);

public record LeadAdminItem(
    Guid Id,
    Guid TreinadorId,
    string Nome,
    TipoContatoLead ContatoTipo,
    string ContatoValor,
    LeadStatus Status,
    bool Anonimizado,
    DateTime CreatedAt);

public class BuscarLeadsPorContatoHandler(
    ILeadRepository leadRepository,
    ILogAprovacaoRepository logAprovacaoRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
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

        var leads = valorNormalizado is null
            ? []
            : await leadRepository.BuscarPorContatoCrossTenantAsync(valorNormalizado, cancellationToken).ConfigureAwait(false);

        await RegistrarAuditoriaAsync(query, cancellationToken).ConfigureAwait(false);

        return leads
            .Select(l => new LeadAdminItem(l.Id, l.TreinadorId, l.Nome, l.Contato.Tipo, l.Contato.Valor, l.Status, l.Anonimizado, l.CreatedAt))
            .ToList();
    }

    // Ação atribuída ao admin (nunca ao titular buscado — a busca cruza tenants e pode não
    // apontar para um único lead). Fail-closed: se o log não puder ser registrado, a busca não
    // pode devolver dado silenciosamente sem rastro (coding §3).
    private async Task RegistrarAuditoriaAsync(BuscarLeadsPorContatoQuery query, CancellationToken cancellationToken)
    {
        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var logResult = LogAprovacao.Registrar(
            TipoAcaoAprovacao.BuscaLeadPorContato,
            query.AdminId,
            query.AdminId,
            "BuscaLeadPorContato",
            agora,
            MascararContato(query.Contato));
        if (logResult.IsFailure)
            throw new InvalidOperationException($"Falha ao registrar auditoria de busca admin de leads: {logResult.Error!.Code}");

        await logAprovacaoRepository.AdicionarAsync(logResult.Value, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string MascararContato(string valor)
    {
        var arroba = valor.IndexOf('@');
        if (arroba > 0)
            return $"{valor[0]}***@{valor[(arroba + 1)..]}";

        return valor.Length <= 4 ? "***" : $"***{valor[^4..]}";
    }

    private static string? NormalizarEmail(string valor)
    {
        var resultado = Email.Criar(valor);
        return resultado.IsSuccess ? resultado.Value.Value : null;
    }
}
