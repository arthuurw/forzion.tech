using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Application.UseCases.Agents.Leads;

public class RegistrarLeadAgenteHandler(
    ITreinadorRepository treinadorRepository,
    ILeadRepository leadRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public virtual Task<Result<StagedLead>> HandleAsync(RegistrarLeadAgenteCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<StagedLead>> HandleAsyncCore(RegistrarLeadAgenteCommand command, CancellationToken cancellationToken)
    {
        if (!command.ConsentGranted)
            return Result.Failure<StagedLead>(LeadAgenteErrors.ConsentimentoNaoConcedido);

        if (string.IsNullOrWhiteSpace(command.Name))
            return Result.Failure<StagedLead>(LeadErrors.NomeObrigatorio);

        var nomeNormalizado = command.Name.Trim();
        if (nomeNormalizado.Length > 200)
            return Result.Failure<StagedLead>(LeadErrors.NomeMuitoLongo);

        if (command.Interest is { Length: > 1000 })
            return Result.Failure<StagedLead>(LeadErrors.InteresseMuitoLongo);

        if (command.IdempotencyKey is { Length: > 200 })
            return Result.Failure<StagedLead>(LeadAgenteErrors.IdempotencyKeyMuitoLonga);

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        var consentimentoResult = ConsentimentoLead.Criar(command.ConsentPurpose, command.ConsentGrantedAt ?? agora, agora);
        if (consentimentoResult.IsFailure)
            return Result.Failure<StagedLead>(consentimentoResult.Error!);

        var origemResult = OrigemLead.Criar(command.OriginUserAgent, command.OriginAssistant);
        if (origemResult.IsFailure)
            return Result.Failure<StagedLead>(origemResult.Error!);

        var tipoContatoResult = ParseTipoContato(command.ContactType);
        if (tipoContatoResult.IsFailure)
            return Result.Failure<StagedLead>(tipoContatoResult.Error!);

        var contatoResult = ContatoLead.Criar(tipoContatoResult.Value, command.ContactValue);
        if (contatoResult.IsFailure)
            return Result.Failure<StagedLead>(contatoResult.Error!);

        var contato = contatoResult.Value;
        var consentimento = consentimentoResult.Value;

        var treinador = await treinadorRepository.ObterPorIdAsync(command.TenantId, cancellationToken).ConfigureAwait(false);
        if (!AgentTenantGuard.EstaPublicado(treinador))
            return Result.Failure<StagedLead>(TreinadorErrors.NaoEncontrado);

        var interesseNormalizado = string.IsNullOrWhiteSpace(command.Interest) ? null : command.Interest.Trim();
        var argumentosHash = IdempotenciaLead.Calcular(nomeNormalizado, contato.Tipo, contato.Valor, interesseNormalizado, consentimento.Finalidade);

        if (!string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            var existente = await leadRepository
                .ObterPorIdempotencyKeyAsync(command.TenantId, command.IdempotencyKey, cancellationToken)
                .ConfigureAwait(false);

            if (existente is not null)
                return existente.ArgumentosHash == argumentosHash
                    ? Result.Success(ProjetarStagedLead(existente))
                    : Result.Failure<StagedLead>(LeadAgenteErrors.IdempotencyConflito);
        }

        var leadResult = Lead.Criar(
            command.TenantId, nomeNormalizado, contato, interesseNormalizado, consentimento, origemResult.Value,
            LeadSource.Agent, command.IdempotencyKey, argumentosHash, agora);
        if (leadResult.IsFailure)
            return Result.Failure<StagedLead>(leadResult.Error!);

        var lead = leadResult.Value;
        await leadRepository.AdicionarAsync(lead, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(ProjetarStagedLead(lead));
    }

    private static Result<TipoContatoLead> ParseTipoContato(string contactType) => contactType switch
    {
        "email" => Result.Success(TipoContatoLead.Email),
        "phone" => Result.Success(TipoContatoLead.Telefone),
        "whatsapp" => Result.Success(TipoContatoLead.WhatsApp),
        _ => Result.Failure<TipoContatoLead>(LeadAgenteErrors.TipoContatoInvalido)
    };

    private static StagedLead ProjetarStagedLead(Lead lead) => new(lead.Id.ToString(), "agent", "pending");
}
