using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Application.UseCases.Leads.CriarLeadManual;

public class CriarLeadManualHandler(
    ILeadRepository leadRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public virtual Task<Result<CriarLeadManualResponse>> HandleAsync(
        CriarLeadManualCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<CriarLeadManualResponse>> HandleAsyncCore(
        CriarLeadManualCommand command,
        CancellationToken cancellationToken)
    {
        var contatoResult = ContatoLead.Criar(command.ContatoTipo, command.ContatoValor);
        if (contatoResult.IsFailure)
            return Result.Failure<CriarLeadManualResponse>(contatoResult.Error!);

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        var consentimentoResult = ConsentimentoLead.Criar(command.ConsentimentoFinalidade, agora, agora);
        if (consentimentoResult.IsFailure)
            return Result.Failure<CriarLeadManualResponse>(consentimentoResult.Error!);

        var leadResult = Lead.Criar(
            command.TreinadorId,
            command.Nome,
            contatoResult.Value,
            command.Interesse,
            consentimentoResult.Value,
            null,
            LeadSource.Manual,
            idempotencyKey: null,
            argumentosHash: null,
            agora);
        if (leadResult.IsFailure)
            return Result.Failure<CriarLeadManualResponse>(leadResult.Error!);

        var lead = leadResult.Value;
        await leadRepository.AdicionarAsync(lead, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new CriarLeadManualResponse(lead.Id));
    }
}
