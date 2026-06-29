using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Treinadores.ContratarPlanoTreinador;

public record ContratarPlanoTreinadorCommand(
    Guid TreinadorId,
    Guid PlanoPlataformaId,
    MetodoPagamento Metodo = MetodoPagamento.Pix);
