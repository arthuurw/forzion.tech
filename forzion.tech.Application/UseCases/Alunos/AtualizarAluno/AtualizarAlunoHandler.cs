using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.CadastrarAluno;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Alunos.AtualizarAluno;

public class AtualizarAlunoHandler(
    IAlunoRepository alunoRepository,
    IUnitOfWork unitOfWork,
    ILogger<AtualizarAlunoHandler> logger)
{
    private readonly IAlunoRepository _alunoRepository = alunoRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<AtualizarAlunoHandler> _logger = logger;

    public virtual async Task<AlunoResponse> HandleAsync(
        AtualizarAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var aluno = await _alunoRepository
            .ObterPorIdAsync(command.AlunoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new AlunoNaoEncontradoException();

        if (aluno.TenantId != command.TenantId)
            throw new AcessoNegadoException();

        if (aluno.Status == AlunoStatus.Inativo)
            throw new AlunoInativoException();

        aluno.Atualizar(command.Nome, command.Email, command.Telefone);

        await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Aluno {AlunoId} atualizado.", aluno.Id);

        return CadastrarAlunoHandler.ToResponse(aluno);
    }
}
