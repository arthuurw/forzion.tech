using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Application.UseCases.Pacotes.AtualizarPacoteAluno;

public class AtualizarPacoteAlunoHandler(
    IPacoteAlunoRepository pacoteRepository,
    IUnitOfWork unitOfWork)
{
    private readonly IPacoteAlunoRepository _pacoteRepository = pacoteRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public virtual async Task<PacoteAlunoResponse> HandleAsync(
        AtualizarPacoteAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var pacote = await _pacoteRepository.ObterPorIdAsync(command.PacoteId, cancellationToken).ConfigureAwait(false)
            ?? throw new PacoteNaoEncontradoException();

        if (pacote.TreinadorId != command.TreinadorId)
            throw new AcessoNegadoException();

        pacote.Atualizar(command.Nome, command.Preco, command.Descricao);

        await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return PacoteAlunoResponseExtensions.ToResponse(pacote);
    }
}
