using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using forzion.tech.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinadores.RegistrarTreinador;

public class RegistrarTreinadorHandler(
    IContaRepository contaRepository,
    ITreinadorRepository treinadorRepository,
    IPlanoPlataformaRepository planoRepository,
    IAssinaturaTreinadorRepository assinaturaTreinadorRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    IValidator<RegistrarTreinadorCommand> validator,
    TimeProvider timeProvider,
    ILogger<RegistrarTreinadorHandler> logger)
{
    public virtual Task<Result<TreinadorResponse>> HandleAsync(
        RegistrarTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<TreinadorResponse>> HandleAsyncCore(
        RegistrarTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var emailExistente = await contaRepository.ObterPorEmailAsync(command.Email, cancellationToken).ConfigureAwait(false);
        if (emailExistente is not null)
            throw new EmailJaCadastradoException();

        var plano = await planoRepository.ObterPorIdAsync(command.PlanoPlataformaId, cancellationToken).ConfigureAwait(false)
            ?? throw new PlanoPlataformaNaoEncontradoException();
        if (!plano.IsAtivo)
            return Result.Failure<TreinadorResponse>(Error.Business("plano_inativo", "Plano indisponível para cadastro."));
        if (plano.Tier == TierPlano.Elite)
            return Result.Failure<TreinadorResponse>(PlanoPlataformaErrors.EliteIndisponivel);

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var pago = plano.Preco > 0;

        var emailResult = Email.Criar(command.Email);
        if (emailResult.IsFailure)
            return Result.Failure<TreinadorResponse>(emailResult.Error!);

        var contaResult = Domain.Entities.Conta.Criar(emailResult.Value, passwordHasher.Hash(command.Senha), TipoConta.Treinador, agora, emitirRegistro: !pago);
        if (contaResult.IsFailure)
            return Result.Failure<TreinadorResponse>(contaResult.Error!);
        var conta = contaResult.Value;

        var treinadorResult = Treinador.Criar(conta.Id, command.Nome, agora, command.Telefone, plano.Id, command.ModoPagamentoAluno, aguardandoPagamento: pago);
        if (treinadorResult.IsFailure)
            return Result.Failure<TreinadorResponse>(treinadorResult.Error!);
        var treinador = treinadorResult.Value;

        await contaRepository.AdicionarAsync(conta, cancellationToken).ConfigureAwait(false);
        await treinadorRepository.AdicionarAsync(treinador, cancellationToken).ConfigureAwait(false);

        if (pago)
        {
            var assinaturaResult = AssinaturaTreinador.Criar(treinador.Id, plano.Id, plano.Preco, agora);
            if (assinaturaResult.IsFailure)
                return Result.Failure<TreinadorResponse>(assinaturaResult.Error!);
            await assinaturaTreinadorRepository.AdicionarAsync(assinaturaResult.Value, cancellationToken).ConfigureAwait(false);
        }

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Treinador {TreinadorId} registrado (plano {PlanoId}, pago={Pago}).", treinador.Id, plano.Id, pago);

        return Result.Success(new TreinadorResponse(treinador.Id, treinador.ContaId, treinador.Nome, treinador.Status, treinador.PlanoPlataformaId, treinador.CreatedAt));
    }
}
