using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Alunos.CadastrarAluno;

public class CadastrarAlunoHandler(
    IAlunoRepository alunoRepository,
    IUnitOfWork unitOfWork,
    IValidator<CadastrarAlunoCommand> validator,
    ILogger<CadastrarAlunoHandler> logger)
{
    private readonly IAlunoRepository _alunoRepository = alunoRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IValidator<CadastrarAlunoCommand> _validator = validator;
    private readonly ILogger<CadastrarAlunoHandler> _logger = logger;

    public virtual async Task<AlunoResponse> HandleAsync(
        CadastrarAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await _validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        var aluno = Aluno.Criar(command.ContaId, command.Nome, command.Email, command.Telefone);

        await _alunoRepository.AdicionarAsync(aluno, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Aluno {AlunoId} cadastrado.", aluno.Id);

        return ToResponse(aluno);
    }

    internal static AlunoResponse ToResponse(Aluno aluno) => new(
        aluno.Id,
        aluno.Nome,
        aluno.Email,
        aluno.Telefone,
        aluno.Status,
        aluno.ContaId,
        aluno.CreatedAt,
        aluno.UpdatedAt,
        aluno.DiasDisponiveis,
        aluno.TempoDisponivelMinutos,
        aluno.Finalidade,
        aluno.FocoTreino,
        aluno.NivelCondicionamento,
        aluno.LimitacoesFisicas,
        aluno.Doencas,
        aluno.ObservacoesAdicionais
    );
}
