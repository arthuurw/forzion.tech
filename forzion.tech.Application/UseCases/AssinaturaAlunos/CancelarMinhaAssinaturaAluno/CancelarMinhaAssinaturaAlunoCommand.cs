namespace forzion.tech.Application.UseCases.AssinaturaAlunos.CancelarMinhaAssinaturaAluno;

/// <summary>
/// Comando emitido pelo endpoint POST <c>/aluno/assinatura/cancelar</c>. O
/// <see cref="AlunoId"/> é derivado do JWT (não vem do cliente) — auto-serviço
/// do próprio aluno.
/// </summary>
public record CancelarMinhaAssinaturaAlunoCommand(Guid AlunoId);
