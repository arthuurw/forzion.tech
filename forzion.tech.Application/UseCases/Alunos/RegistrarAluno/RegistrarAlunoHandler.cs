using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
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
    IPacoteRepository pacoteRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    ILogAprovacaoRepository logAprovacaoRepository,
    IValidator<RegistrarAlunoCommand> validator,
    TimeProvider timeProvider,
    ILogger<RegistrarAlunoHandler> logger)
{
    public virtual Task<Result<AlunoResponse>> HandleAsync(
        RegistrarAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<AlunoResponse>> HandleAsyncCore(
        RegistrarAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        // Defense-in-depth (LGPD art. 11): o validator já barra coleta sem consentimento,
        // mas se for contornado o handler NÃO pode persistir dados de saúde sem consentimento.
        if (command.ColetaDadosSaude && !command.ConsentimentoDadosSaude)
            return Result.Failure<AlunoResponse>(AlunoErrors.ConsentimentoSaudeObrigatorio);

        var emailExistente = await contaRepository.ObterPorEmailAsync(command.Email, cancellationToken).ConfigureAwait(false);
        if (emailExistente is not null)
            throw new EmailJaCadastradoException();

        var treinador = await treinadorRepository.ObterPorIdAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false)
            ?? throw new TreinadorNaoEncontradoException();

        if (treinador.Status != TreinadorStatus.Ativo)
            return Result.Failure<AlunoResponse>(TreinadorErrors.NaoDisponivel);

        var pacote = await pacoteRepository.ObterPorIdAsync(command.PacoteId, cancellationToken).ConfigureAwait(false)
            ?? throw new PacoteNaoEncontradoException();

        if (pacote.TreinadorId != command.TreinadorId)
            return Result.Failure<AlunoResponse>(Error.Business("pacote.nao_pertence_treinador", "O pacote informado não pertence ao treinador selecionado."));

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var emailResult = Email.Criar(command.Email);
        if (emailResult.IsFailure)
            return Result.Failure<AlunoResponse>(emailResult.Error!);

        var contaResult = Domain.Entities.Conta.Criar(emailResult.Value, passwordHasher.Hash(command.Senha), TipoConta.Aluno, agora);
        if (contaResult.IsFailure)
            return Result.Failure<AlunoResponse>(contaResult.Error!);
        var conta = contaResult.Value;

        var tempoDisponivel = command.TempoDisponivelMinutos.HasValue
            ? (TempoDisponivel?)command.TempoDisponivelMinutos.Value
            : null;
        var alunoResult = Aluno.Criar(
            conta.Id,
            command.Nome,
            agora,
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
        if (alunoResult.IsFailure)
            return Result.Failure<AlunoResponse>(alunoResult.Error!);
        var aluno = alunoResult.Value;

        var vinculoResult = VinculoTreinadorAluno.Criar(command.TreinadorId, aluno.Id, agora, command.PacoteId);
        if (vinculoResult.IsFailure)
            return Result.Failure<AlunoResponse>(vinculoResult.Error!);
        var vinculo = vinculoResult.Value;

        await contaRepository.AdicionarAsync(conta, cancellationToken).ConfigureAwait(false);
        await alunoRepository.AdicionarAsync(aluno, cancellationToken).ConfigureAwait(false);
        await vinculoRepository.AdicionarAsync(vinculo, cancellationToken).ConfigureAwait(false);

        if (command.ColetaDadosSaude)
        {
            var observacao = command.ConsentimentoDadosSaudeEm is { } reportado
                ? $"v1; cliente reportou: {reportado.ToUniversalTime():o}"
                : "v1";

            var consentLog = LogAprovacao.Registrar(
                TipoAcaoAprovacao.ConsentimentoAnamnese,
                realizadoPorId: conta.Id,
                entidadeId: conta.Id,
                entidadeTipo: "Conta",
                agora,
                observacao);
            if (consentLog.IsFailure)
                return Result.Failure<AlunoResponse>(consentLog.Error!);

            await logAprovacaoRepository.AdicionarAsync(consentLog.Value, cancellationToken).ConfigureAwait(false);
        }

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Aluno {AlunoId} registrado com vínculo pendente ao treinador {TreinadorId}.", aluno.Id, command.TreinadorId);

        return Result.Success(CadastrarAluno.CadastrarAlunoHandler.ToResponse(aluno));
    }
}
