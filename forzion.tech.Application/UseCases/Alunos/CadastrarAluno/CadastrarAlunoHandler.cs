using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Alunos.CadastrarAluno;

// TODO: refatorar — remover TenantId/TreinadorId ao concluir Fase 2 do domínio
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

        var aluno = Aluno.Criar(command.Nome, command.TenantId, command.TreinadorId, command.Email, command.Telefone);

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
        aluno.TenantId,
        aluno.TreinadorId,
        aluno.CreatedAt,
        aluno.UpdatedAt
    );
}
