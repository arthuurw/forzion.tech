using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Results;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.AssinaturaAlunos.CancelarMinhaAssinaturaAluno;

/// <summary>
/// Auto-cancelamento de assinatura pelo aluno autenticado. Busca a assinatura
/// "atual" do aluno (Ativa ou Inadimplente — qualquer status não-cancelado
/// retornado por <see cref="IAssinaturaAlunoRepository.ObterAtualPorAlunoAsync"/>),
/// invoca <c>Cancelar(agora)</c> no agregado (dispara
/// <see cref="Domain.Events.AssinaturaAlunoCanceladaEvent"/>) e commita.
///
/// Falhas:
/// <list type="bullet">
///   <item><description>Nenhuma assinatura ativa/inadimplente → <c>not_found</c> (mapeia 404 no endpoint).</description></item>
///   <item><description>Já cancelada (race) → <c>business_error</c> (mapeia 422).</description></item>
/// </list>
/// </summary>
public class CancelarMinhaAssinaturaAlunoHandler(
    IAssinaturaAlunoRepository assinaturaRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<CancelarMinhaAssinaturaAlunoHandler> logger)
{
    public const string AssinaturaNaoEncontradaErrorCode = "assinatura_nao_encontrada";

    public virtual async Task<Result> HandleAsync(
        CancelarMinhaAssinaturaAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var assinatura = await assinaturaRepository
            .ObterAtualPorAlunoAsync(command.AlunoId, cancellationToken)
            .ConfigureAwait(false);

        if (assinatura is null || assinatura.Status == AssinaturaAlunoStatus.Cancelada)
            return Result.Failure(new Error(
                AssinaturaNaoEncontradaErrorCode,
                "Nenhuma assinatura ativa encontrada para cancelar."));

        try
        {
            assinatura.Cancelar(timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (DomainException ex)
        {
            return Result.Failure(Error.Business(ex.Message));
        }

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Aluno {AlunoId} cancelou a própria assinatura {AssinaturaAlunoId}.",
            command.AlunoId, assinatura.Id);

        return Result.Success();
    }
}
