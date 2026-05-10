using FluentValidation;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Treinos.CriarTreino;

public class CriarTreinoHandler(
    ITreinoRepository treinoRepository,
    ITreinoAlunoRepository treinoAlunoRepository,
    IAlunoRepository alunoRepository,
    IVinculoTreinadorAlunoRepository vinculoRepository,
    IUnitOfWork unitOfWork,
    IUserContext userContext,
    IValidator<CriarTreinoCommand> validator,
    ILogger<CriarTreinoHandler> logger)
{
    private readonly ITreinoRepository _treinoRepository = treinoRepository;
    private readonly ITreinoAlunoRepository _treinoAlunoRepository = treinoAlunoRepository;
    private readonly IAlunoRepository _alunoRepository = alunoRepository;
    private readonly IVinculoTreinadorAlunoRepository _vinculoRepository = vinculoRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserContext _userContext = userContext;
    private readonly IValidator<CriarTreinoCommand> _validator = validator;
    private readonly ILogger<CriarTreinoHandler> _logger = logger;

    public virtual async Task<TreinoResponse> HandleAsync(
        CriarTreinoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await _validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);

        if (command.AlunoId.HasValue)
        {
            _ = await _alunoRepository
                .ObterPorIdAsync(command.AlunoId.Value, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new AlunoNaoEncontradoException();

            if (!_userContext.IsSystemAdmin)
            {
                var vinculo = await _vinculoRepository
                    .ObterAtivoAsync(_userContext.PerfilId, command.AlunoId.Value, cancellationToken)
                    .ConfigureAwait(false);

                if (vinculo is null)
                    throw new AcessoNegadoException();
            }
        }

        var treino = Treino.Criar(command.Nome, command.Objetivo, command.TreinadorId, command.Dificuldade, command.DataInicio, command.DataFim);
        await _treinoRepository.AdicionarAsync(treino, cancellationToken).ConfigureAwait(false);

        if (command.AlunoId.HasValue)
        {
            var treinoAluno = TreinoAluno.Criar(treino.Id, command.AlunoId.Value);
            await _treinoAlunoRepository.AdicionarAsync(treinoAluno, cancellationToken).ConfigureAwait(false);
        }

        await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Treino {TreinoId} criado{Aluno}.",
            treino.Id,
            command.AlunoId.HasValue ? $" para o aluno {command.AlunoId.Value}" : " sem aluno vinculado");

        return TreinoResponseExtensions.ToResponse(treino);
    }
}
