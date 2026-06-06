using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Treinadores.TrocarPlanoTreinador;

public record TrocarPlanoTreinadorCommand(
    Guid TreinadorId,
    Guid NovoPlanoPlataformaId,
    MetodoPagamento Metodo = MetodoPagamento.Pix);
