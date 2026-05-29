using forzion.tech.Domain.Enums;

namespace forzion.tech.Domain.Events;

/// <summary>
/// Disparado em <see cref="Entities.Pagamento.Criar"/>. Notifica o aluno sobre cobrança
/// pendente (via e-mail + WhatsApp) com link pro portal.
///
/// Carrega só identificadores + dados essenciais; handler resolve aluno/contato via
/// <c>AssinaturaAlunoId</c> → <c>IAssinaturaAlunoRepository</c> → <c>IAlunoRepository</c>
/// → fallback <c>Conta.Email</c>/<c>Aluno.Telefone</c>.
/// </summary>
public sealed record PagamentoCriadoEvent(
    Guid PagamentoId,
    Guid AssinaturaAlunoId,
    decimal Valor,
    MetodoPagamento MetodoPagamento,
    DateTime OcorridoEm) : IDomainEvent;
