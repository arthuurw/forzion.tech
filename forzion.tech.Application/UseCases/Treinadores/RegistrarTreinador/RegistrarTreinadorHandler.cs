using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinadores.RegistrarTreinador;

public class RegistrarTreinadorHandler(
    IContaRepository contaRepository,
    ITreinadorRepository treinadorRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    IValidator<RegistrarTreinadorCommand> validator,
    TimeProvider timeProvider,
    ILogger<RegistrarTreinadorHandler> logger)
{
    public virtual Task<TreinadorResponse> HandleAsync(
        RegistrarTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<TreinadorResponse> HandleAsyncCore(
        RegistrarTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var emailExistente = await contaRepository.ObterPorEmailAsync(command.Email, cancellationToken).ConfigureAwait(false);
        if (emailExistente is not null)
            throw new EmailJaCadastradoException();

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var conta = Domain.Entities.Conta.Criar(Email.Criar(command.Email), passwordHasher.Hash(command.Senha), TipoConta.Treinador, agora);
        var treinador = Treinador.Criar(conta.Id, command.Nome, agora, command.Telefone);

        await contaRepository.AdicionarAsync(conta, cancellationToken).ConfigureAwait(false);
        await treinadorRepository.AdicionarAsync(treinador, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Treinador {TreinadorId} registrado para conta {ContaId}.", treinador.Id, conta.Id);

        return new TreinadorResponse(treinador.Id, treinador.ContaId, treinador.Nome, treinador.Status, treinador.PlanoPlataformaId, treinador.CreatedAt);
    }
}
