using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.CadastrarAluno;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Alunos.AlterarStatusAluno;

public class AlterarStatusAlunoHandler(
    IAlunoRepository alunoRepository,
    IUserContext userContext,
    IUnitOfWork unitOfWork,
    ILogger<AlterarStatusAlunoHandler> logger)
{
    private readonly IAlunoRepository _alunoRepository = alunoRepository;
    private readonly IUserContext _userContext = userContext;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<AlterarStatusAlunoHandler> _logger = logger;

    public virtual async Task<AlunoResponse> HandleAsync(
        AlterarStatusAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var aluno = await _alunoRepository
            .ObterPorIdAsync(command.AlunoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new AlunoNaoEncontradoException();

        // Validação de autorização: apenas SystemAdmin pode alterar status de alunos
        if (!_userContext.IsSystemAdmin)
            throw new AcessoNegadoException();

        aluno.AlterarStatus(command.NovoStatus);

        await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Status do aluno {AlunoId} alterado para {Status}.", aluno.Id, command.NovoStatus);

        return CadastrarAlunoHandler.ToResponse(aluno);
    }
}
