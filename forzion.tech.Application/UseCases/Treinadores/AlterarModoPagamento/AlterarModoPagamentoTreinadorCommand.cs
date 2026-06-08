using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Treinadores.AlterarModoPagamento;

public record AlterarModoPagamentoTreinadorCommand(Guid TreinadorId, ModoPagamentoAluno NovoModo);

public record AlterarModoPagamentoResponse(
    ModoPagamentoAluno Modo,
    DateTime AlteradoEm,
    int AssinaturasCriadas = 0,
    int VinculosIgnorados = 0);
