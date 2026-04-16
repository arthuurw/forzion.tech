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
    IUnitOfWork unitOfWork,
    IValidator<CriarTreinoCommand> validator,
    ILogger<CriarTreinoHandler> logger)
{
    private readonly ITreinoRepository _treinoRepository = treinoRepository;
    private readonly ITreinoAlunoRepository _treinoAlunoRepository = treinoAlunoRepository;
    private readonly IAlunoRepository _alunoRepository = alunoRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IValidator<CriarTreinoCommand> _validator = validator;
    private readonly ILogger<CriarTreinoHandler> _logger = logger;

    public virtual async Task<TreinoResponse> HandleAsync(
        CriarTreinoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation("Iniciando criação de treino: Nome={Nome}, Objetivo={Objetivo}, AlunoId={AlunoId}, TenantId={TenantId}",
            command.Nome, command.Objetivo, command.AlunoId, command.TenantId);

        await _validator.ValidateAndThrowAsync(command, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Validação do comando concluída com sucesso para treino {Nome}", command.Nome);

        var aluno = await _alunoRepository
            .ObterPorIdAsync(command.AlunoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new AlunoNaoEncontradoException();
        _logger.LogInformation("Aluno obtido: AlunoId={AlunoId}, Nome={Nome}, TenantId={TenantId}",
            aluno.Id, aluno.Nome, aluno.TenantId);

        if (aluno.TenantId != command.TenantId)
        {
            _logger.LogWarning("Validação de acesso falhou: AlunoTenantId={AlunoTenantId} diferente do CommandTenantId={CommandTenantId}",
                aluno.TenantId, command.TenantId);
            throw new AcessoNegadoException();
        }
        _logger.LogInformation("Validação de acesso bem-sucedida para TenantId={TenantId}", command.TenantId);

        var treino = Treino.Criar(command.Nome, command.Objetivo, command.TenantId, command.TreinadorId);
        var vinculo = TreinoAluno.Criar(treino.Id, command.AlunoId);
        _logger.LogInformation("Treino e vínculo criados: TreinoId={TreinoId}, Nome={Nome}, AlunoId={AlunoId}",
            treino.Id, treino.Nome, command.AlunoId);

        await _treinoRepository.AdicionarAsync(treino, cancellationToken).ConfigureAwait(false);
        await _treinoAlunoRepository.AdicionarAsync(vinculo, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Treino e vínculo adicionados aos repositórios: TreinoId={TreinoId}, VinculoId={VinculoId}",
            treino.Id, vinculo.Id);

        await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Transação persistida com sucesso. Treino {TreinoId} criado para o aluno {AlunoId}.",
            treino.Id, command.AlunoId);

        return TreinoResponseExtensions.ToResponse(treino);
    }
}
