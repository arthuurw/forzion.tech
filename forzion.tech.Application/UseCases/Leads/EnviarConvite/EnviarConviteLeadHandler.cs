using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Application.UseCases.Leads.EnviarConvite;

public class EnviarConviteLeadHandler(
    ILeadRepository leadRepository,
    ILeadConviteRepository leadConviteRepository,
    ITreinadorRepository treinadorRepository,
    ILeadConviteSender leadConviteSender,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    private const int ValidadeDias = 14;

    public virtual Task<Result<EnviarConviteLeadResponse>> HandleAsync(
        EnviarConviteLeadCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<EnviarConviteLeadResponse>> HandleAsyncCore(
        EnviarConviteLeadCommand command,
        CancellationToken cancellationToken)
    {
        var lead = await leadRepository
            .ObterComHistoricoAsync(command.TreinadorId, command.LeadId, cancellationToken)
            .ConfigureAwait(false);
        if (lead is null)
            return Result.Failure<EnviarConviteLeadResponse>(LeadErrors.NaoEncontrado);

        if (lead.Anonimizado)
            return Result.Failure<EnviarConviteLeadResponse>(LeadErrors.Anonimizado);

        if (lead.Status is LeadStatus.Convertido or LeadStatus.Descartado)
            return Result.Failure<EnviarConviteLeadResponse>(LeadErrors.EstadoTerminal);

        if (lead.Contato.Tipo == TipoContatoLead.Telefone)
            return Result.Failure<EnviarConviteLeadResponse>(LeadConviteErrors.ContatoNaoSuportaConvite);

        var treinador = await treinadorRepository
            .ObterPorIdAsync(command.TreinadorId, cancellationToken)
            .ConfigureAwait(false);
        if (treinador is null)
            return Result.Failure<EnviarConviteLeadResponse>(LeadErrors.NaoEncontrado);

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        var conviteAtivo = await leadConviteRepository
            .ObterAtivoPorLeadAsync(command.TreinadorId, command.LeadId, cancellationToken)
            .ConfigureAwait(false);
        conviteAtivo?.Invalidar(agora);

        var tokenCru = LeadConviteToken.Gerar();
        var conviteResult = LeadConvite.Criar(
            command.LeadId, command.TreinadorId, LeadConviteToken.Hash(tokenCru), agora.AddDays(ValidadeDias), agora);
        if (conviteResult.IsFailure)
            return Result.Failure<EnviarConviteLeadResponse>(conviteResult.Error!);

        await leadConviteRepository.AdicionarAsync(conviteResult.Value, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        var enviado = await leadConviteSender
            .EnviarAsync(lead.Contato.Tipo, lead.Contato.Valor, lead.Nome, treinador.Nome, tokenCru, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new EnviarConviteLeadResponse(enviado));
    }
}
