using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Results;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Pacotes.ExcluirPacoteAluno;

public class ExcluirPacoteAlunoHandler(
    IPacoteAlunoRepository pacoteRepository,
    IUnitOfWork unitOfWork)
{
    private readonly IPacoteAlunoRepository _pacoteRepository = pacoteRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public virtual async Task<Result> HandleAsync(
        ExcluirPacoteAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var pacote = await _pacoteRepository.ObterPorIdAsync(command.PacoteId, cancellationToken).ConfigureAwait(false)
            ?? throw new PacoteNaoEncontradoException();

        if (pacote.TreinadorId != command.TreinadorId)
            throw new AcessoNegadoException();

        var temVinculos = await _pacoteRepository.ExisteVinculoComPacoteAsync(command.PacoteId, cancellationToken).ConfigureAwait(false);
        if (temVinculos)
            return Result.Failure(Error.Business("Não é possível excluir um pacote com alunos vinculados."));

        _pacoteRepository.Remover(pacote);
        await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
