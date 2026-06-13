using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Application.UseCases.Suporte.EnviarMensagem;

public class EnviarMensagemSuporteHandler(
    IUserContext userContext,
    IContaRepository contaRepository,
    IMensagemSuporteRepository mensagemSuporteRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IValidator<EnviarMensagemSuporteCommand> validator)
{
    public virtual Task<Result> HandleAsync(
        EnviarMensagemSuporteCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result> HandleAsyncCore(
        EnviarMensagemSuporteCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        // O validator já garante o nome — TryParse só converte para o enum (case-insensitive
        // para casar com IsEnumName(caseSensitive: false)).
        Enum.TryParse<CategoriaSuporte>(command.Categoria, ignoreCase: true, out var categoria);

        var contaId = userContext.ContaId;
        var conta = await contaRepository.ObterPorIdAsync(contaId, cancellationToken).ConfigureAwait(false);
        if (conta is null)
            return Result.Failure(Error.NotFound("conta.nao_encontrada", "Conta autenticada não encontrada."));

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var criarResult = MensagemSuporte.Criar(contaId, categoria, command.Assunto, command.Descricao, agora);
        if (criarResult.IsFailure)
            return Result.Failure(criarResult.Error!);

        await mensagemSuporteRepository.AdicionarAsync(criarResult.Value, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
