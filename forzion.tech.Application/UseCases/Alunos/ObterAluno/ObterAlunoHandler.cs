using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Alunos.ObterAluno;

public class ObterAlunoHandler(
    IAlunoRepository alunoRepository,
    ILogger<ObterAlunoHandler> logger)
{
    private readonly IAlunoRepository _alunoRepository = alunoRepository;
    private readonly ILogger<ObterAlunoHandler> _logger = logger;

    public virtual async Task<AlunoResponse> HandleAsync(
        ObterAlunoQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var aluno = await _alunoRepository
            .ObterPorIdAsync(query.AlunoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new AlunoNaoEncontradoException();

        // TODO (Fase 5): validar autorização via IUserContext
        _logger.LogInformation("Aluno {AlunoId} consultado.", aluno.Id);

        return CadastrarAluno.CadastrarAlunoHandler.ToResponse(aluno);
    }
}
