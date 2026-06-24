using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Planos.ExcluirPlanoPlataforma;

public class ExcluirPlanoPlataformaHandler(
    IPlanoPlataformaRepository planoRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogAprovacaoRepository logRepository,
    ILogger<ExcluirPlanoPlataformaHandler> logger,
    IUserContext userContext)
{
    public virtual Task<Result> HandleAsync(
        ExcluirPlanoPlataformaCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result> HandleAsyncCore(
        ExcluirPlanoPlataformaCommand command,
        CancellationToken cancellationToken = default)
    {
        var plano = await planoRepository.ObterPorIdAsync(command.PlanoId, cancellationToken).ConfigureAwait(false);
        if (plano is null)
            return Result.Failure(Error.NotFound("plano_nao_encontrado", "Plano não encontrado."));

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        plano.Inativar(agora);

        var logResult = await logRepository.RegistrarAsync(
            TipoAcaoAprovacao.InativacaoPlanoPlataforma,
            userContext.PerfilId,
            plano.Id,
            nameof(PlanoPlataforma),
            agora,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (logResult.IsFailure)
            return Result.Failure(logResult.Error!);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("PlanoPlataforma {PlanoId} inativado por {AtorId}.", plano.Id, userContext.PerfilId);

        return Result.Success();
    }
}
