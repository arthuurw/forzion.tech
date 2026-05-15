using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Results;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Alunos.RegistrarAluno;

public class RegistrarAlunoHandler(
    IContaRepository contaRepository,
    IAlunoRepository alunoRepository,
    IVinculoTreinadorAlunoRepository vinculoRepository,
    ITreinadorRepository treinadorRepository,
    IPacoteAlunoRepository pacoteRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    IValidator<RegistrarAlunoCommand> validator,
    IWhatsAppNotifier whatsAppNotifier,
    ILogger<RegistrarAlunoHandler> logger)
{
    public virtual async Task<Result<AlunoResponse>> HandleAsync(
        RegistrarAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var emailExistente = await contaRepository.ObterPorEmailAsync(command.Email, cancellationToken).ConfigureAwait(false);
        if (emailExistente is not null)
            throw new EmailJaCadastradoException();

        var treinador = await treinadorRepository.ObterPorIdAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false)
            ?? throw new TreinadorNaoEncontradoException();

        if (treinador.Status != TreinadorStatus.Ativo)
            throw new DomainException("Treinador não disponível para novos alunos.");

        var pacote = await pacoteRepository.ObterPorIdAsync(command.PacoteId, cancellationToken).ConfigureAwait(false)
            ?? throw new PacoteNaoEncontradoException();

        if (pacote.TreinadorId != command.TreinadorId)
            return Result.Failure<AlunoResponse>(Error.Business("O pacote informado não pertence ao treinador selecionado."));

        var conta = Domain.Entities.Conta.Criar(Email.Criar(command.Email), passwordHasher.Hash(command.Senha), TipoConta.Aluno);
        var tempoDisponivel = command.TempoDisponivelMinutos.HasValue
            ? (TempoDisponivel?)command.TempoDisponivelMinutos.Value
            : null;
        var aluno = Aluno.Criar(
            conta.Id,
            command.Nome,
            null,
            command.Telefone,
            command.DiasDisponiveis,
            tempoDisponivel,
            command.Finalidade,
            command.FocoTreino,
            command.NivelCondicionamento,
            command.LimitacoesFisicas,
            command.Doencas,
            command.ObservacoesAdicionais);
        var vinculo = VinculoTreinadorAluno.Criar(command.TreinadorId, aluno.Id, command.PacoteId);

        await contaRepository.AdicionarAsync(conta, cancellationToken).ConfigureAwait(false);
        await alunoRepository.AdicionarAsync(aluno, cancellationToken).ConfigureAwait(false);
        await vinculoRepository.AdicionarAsync(vinculo, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Aluno {AlunoId} registrado com vínculo pendente ao treinador {TreinadorId}.", aluno.Id, command.TreinadorId);

        if (!string.IsNullOrWhiteSpace(treinador.Telefone))
        {
            await whatsAppNotifier.SendAsync(
                treinador.Telefone,
                $"Novo aluno aguardando aprovação: {command.Nome}. Acesse o app para aprovar o vínculo.",
                cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(CadastrarAluno.CadastrarAlunoHandler.ToResponse(aluno));
    }
}
