using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos;
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
    ILogger<RegistrarAlunoHandler> logger)
{
    public virtual async Task<AlunoResponse> HandleAsync(
        RegistrarAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var emailExistente = await contaRepository.ObterPorEmailAsync(command.Email, cancellationToken).ConfigureAwait(false);
        if (emailExistente is not null)
            throw new EmailJaCadastradoException();

        _ = await treinadorRepository.ObterPorIdAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false)
            ?? throw new TreinadorNaoEncontradoException();

        var pacote = await pacoteRepository.ObterPorIdAsync(command.PacoteId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException("Pacote de aluno não encontrado.");

        if (pacote.TreinadorId != command.TreinadorId)
            throw new DomainException("O pacote informado não pertence ao treinador selecionado.");

        var conta = Domain.Entities.Conta.Criar(Email.Criar(command.Email), passwordHasher.Hash(command.Senha), TipoConta.Aluno);
        var aluno = Aluno.Criar(conta.Id, command.Nome, null, command.Telefone);
        var vinculo = VinculoTreinadorAluno.Criar(command.TreinadorId, aluno.Id, command.PacoteId);

        await contaRepository.AdicionarAsync(conta, cancellationToken).ConfigureAwait(false);
        await alunoRepository.AdicionarAsync(aluno, cancellationToken).ConfigureAwait(false);
        await vinculoRepository.AdicionarAsync(vinculo, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Aluno {AlunoId} registrado com vínculo pendente ao treinador {TreinadorId}.", aluno.Id, command.TreinadorId);

        return new AlunoResponse(aluno.Id, aluno.Nome, aluno.Email, aluno.Telefone, aluno.Status, aluno.ContaId, aluno.CreatedAt, aluno.UpdatedAt);
    }
}
