namespace forzion.tech.Domain.Events;

/// <summary>
/// Disparado quando uma <see cref="Entities.AssinaturaAluno"/> transiciona para o
/// status Cancelada via o método <c>Cancelar(agora)</c>. Cobre auto-cancelamento
/// pelo aluno (portal), cancelamento via desvinculação pelo treinador e demais
/// caminhos administrativos.
///
/// Handlers notificam aluno (confirmação) e treinador (perda de receita).
/// </summary>
public sealed record AssinaturaAlunoCanceladaEvent(
    Guid AssinaturaAlunoId,
    Guid AlunoId,
    Guid TreinadorId,
    decimal Valor,
    DateTime OcorridoEm) : IDomainEvent;
