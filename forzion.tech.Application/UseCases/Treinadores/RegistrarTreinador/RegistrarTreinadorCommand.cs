using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Treinadores.RegistrarTreinador;

public record RegistrarTreinadorCommand(
    string Email,
    string Senha,
    string Nome,
    Guid PlanoPlataformaId,
    ModoPagamentoAluno ModoPagamentoAluno,
    string? Telefone = null);
