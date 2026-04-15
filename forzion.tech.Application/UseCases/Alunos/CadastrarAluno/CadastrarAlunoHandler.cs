using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Alunos.CadastrarAluno;

public class CadastrarAlunoHandler(
    IAlunoRepository alunoRepository,
    IUsuarioRepository usuarioRepository,
    IUnitOfWork unitOfWork,
    ILogger<CadastrarAlunoHandler> logger)
{
    private readonly IAlunoRepository _alunoRepository = alunoRepository;
    private readonly IUsuarioRepository _usuarioRepository = usuarioRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<CadastrarAlunoHandler> _logger = logger;

    public virtual async Task<AlunoResponse> HandleAsync(
        CadastrarAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var treinador = await _usuarioRepository
            .ObterPorIdAsync(command.TreinadorId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new UsuarioNaoEncontradoException();

        if (treinador.TenantId != command.TenantId)
            throw new AcessoNegadoException();

        var aluno = Aluno.Criar(command.Nome, command.TenantId, command.TreinadorId, command.Email, command.Telefone);

        await _alunoRepository.AdicionarAsync(aluno, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Aluno {AlunoId} cadastrado pelo treinador {TreinadorId}.", aluno.Id, command.TreinadorId);

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
