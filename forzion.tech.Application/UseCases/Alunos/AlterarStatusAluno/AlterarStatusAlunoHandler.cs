using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.UseCases.Alunos.CadastrarAluno;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Alunos.AlterarStatusAluno;

public class AlterarStatusAlunoHandler(
    IAlunoRepository alunoRepository,
    IUsuarioRepository usuarioRepository,
    IUnitOfWork unitOfWork,
    ILogger<AlterarStatusAlunoHandler> logger)
{
    private readonly IAlunoRepository _alunoRepository = alunoRepository;
    private readonly IUsuarioRepository _usuarioRepository = usuarioRepository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<AlterarStatusAlunoHandler> _logger = logger;

    public virtual async Task<AlunoResponse> HandleAsync(
        AlterarStatusAlunoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var admin = await _usuarioRepository
            .ObterPorIdAsync(command.AdminId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new UsuarioNaoEncontradoException();

        if (admin.Status == UsuarioStatus.Inativo)
            throw new UsuarioInativoException();

        if (admin.Role != Role.Admin || admin.TenantId != command.TenantId)
            throw new AcessoNegadoException();

        var aluno = await _alunoRepository
            .ObterPorIdAsync(command.AlunoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new AlunoNaoEncontradoException();

        if (aluno.TenantId != command.TenantId)
            throw new AcessoNegadoException();

        aluno.AlterarStatus(command.NovoStatus);

        await _unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Status do aluno {AlunoId} alterado para {Status} pelo admin {AdminId}.",
            aluno.Id, command.NovoStatus, command.AdminId);

        return CadastrarAlunoHandler.ToResponse(aluno);
    }
}
