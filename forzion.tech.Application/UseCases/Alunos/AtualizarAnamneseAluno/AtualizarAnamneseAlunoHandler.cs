using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.CadastrarAluno;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Alunos.AtualizarAnamneseAluno;

public class AtualizarAnamneseAlunoHandler(
    IAlunoRepository alunoRepository,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    ILogAprovacaoRepository logAprovacaoRepository,
    IValidator<AtualizarAnamneseAlunoCommand> validator,
    TimeProvider timeProvider,
    ILogger<AtualizarAnamneseAlunoHandler> logger)
{
    public virtual Task<Result<AlunoResponse>> HandleAsync(
        AtualizarAnamneseAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<AlunoResponse>> HandleAsyncCore(
        AtualizarAnamneseAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var aluno = await alunoRepository
            .ObterPorIdAsync(command.AlunoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new AlunoNaoEncontradoException();

        if (!userContext.IsAluno || userContext.PerfilId != aluno.Id)
            throw new AcessoNegadoException();

        // Defense-in-depth (LGPD art. 11): mesma regra do registro — o validator já barra,
        // mas se for contornado o handler NÃO pode persistir dados de saúde sem consentimento.
        if (command.ColetaDadosSaude && !command.ConsentimentoDadosSaude)
            return Result.Failure<AlunoResponse>(AlunoErrors.ConsentimentoSaudeObrigatorio);

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var tempoDisponivel = command.TempoDisponivelMinutos.HasValue
            ? (TempoDisponivel?)command.TempoDisponivelMinutos.Value
            : null;

        var atualizarResult = aluno.AtualizarAnamnese(
            command.DiasDisponiveis,
            tempoDisponivel,
            command.Finalidade,
            command.FocoTreino,
            command.NivelCondicionamento,
            command.LimitacoesFisicas,
            command.Doencas,
            command.ObservacoesAdicionais,
            agora);
        if (atualizarResult.IsFailure)
            return Result.Failure<AlunoResponse>(atualizarResult.Error!);

        if (command.ColetaDadosSaude)
        {
            var observacao = command.ConsentimentoDadosSaudeEm is { } reportado
                ? $"v1; cliente reportou: {reportado.ToUniversalTime():o}"
                : "v1";

            var consentLog = await logAprovacaoRepository.RegistrarAsync(
                TipoAcaoAprovacao.ConsentimentoAnamnese,
                realizadoPorId: aluno.ContaId,
                entidadeId: aluno.ContaId,
                entidadeTipo: "Conta",
                agora,
                observacao,
                cancellationToken).ConfigureAwait(false);
            if (consentLog.IsFailure)
                return Result.Failure<AlunoResponse>(consentLog.Error!);
        }

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Anamnese do aluno {AlunoId} atualizada.", aluno.Id);

        return Result.Success(CadastrarAlunoHandler.ToResponse(aluno));
    }
}
