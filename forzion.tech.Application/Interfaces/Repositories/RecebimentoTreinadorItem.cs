using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.Interfaces.Repositories;

public sealed record RecebimentoTreinadorItem(
    Guid PagamentoId,
    decimal Valor,
    PagamentoStatus Status,
    MetodoPagamento Metodo,
    string NomeAluno,
    DateTime CreatedAt,
    DateTime? DataPagamento);
