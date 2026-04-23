using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Alunos.ObterAluno;

public class ObterAlunoHandler(
    IAlunoRepository alunoRepository,
    IVinculoTreinadorAlunoRepository vinculoRepository,
    IUserContext userContext,
    ILogger<ObterAlunoHandler> logger)
{
    private readonly IAlunoRepository _alunoRepository = alunoRepository;
    private readonly IVinculoTreinadorAlunoRepository _vinculoRepository = vinculoRepository;
    private readonly IUserContext _userContext = userContext;
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

        // Validar autorização
        if (!_userContext.IsSystemAdmin && _userContext.PerfilId != aluno.Id)
        {
            if (_userContext.IsTreinador)
            {
                _ = await _vinculoRepository
                    .ObterAtivoAsync(_userContext.PerfilId, aluno.Id, cancellationToken)
                    .ConfigureAwait(false) ?? throw new AcessoNegadoException();
            }
            else
            {
                throw new AcessoNegadoException();
            }
        }

        _logger.LogInformation("Aluno {AlunoId} consultado.", aluno.Id);

        return CadastrarAluno.CadastrarAlunoHandler.ToResponse(aluno);
    }
}
