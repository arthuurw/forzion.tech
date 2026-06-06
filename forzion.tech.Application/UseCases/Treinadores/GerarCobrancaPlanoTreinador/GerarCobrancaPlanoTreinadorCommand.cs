using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Treinadores.GerarCobrancaPlanoTreinador;

public record GerarCobrancaPlanoTreinadorCommand(
    Guid AssinaturaTreinadorId,
    MetodoPagamento Metodo = MetodoPagamento.Pix);
