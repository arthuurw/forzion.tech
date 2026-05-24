using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Results;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.AssinaturaAlunos.CancelarAssinaturaAluno;

public class CancelarAssinaturaAlunoHandler(
    IAssinaturaAlunoRepository assinaturaRepository,
    IUnitOfWork unitOfWork,
    ILogger<CancelarAssinaturaAlunoHandler> logger)
{
    public virtual async Task<Result> HandleAsync(
        CancelarAssinaturaAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var assinatura = await assinaturaRepository.ObterPorIdAsync(command.AssinaturaAlunoId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException("AssinaturaAluno não encontrada.");

        try
        {
            assinatura.Cancelar();
        }
        catch (DomainException ex)
        {
            return Result.Failure(Error.Business(ex.Message));
        }

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("AssinaturaAluno {AssinaturaAlunoId} cancelada.", assinatura.Id);

        return Result.Success();
    }
}
