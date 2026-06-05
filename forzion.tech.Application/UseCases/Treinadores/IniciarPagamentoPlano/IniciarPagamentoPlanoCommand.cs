using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Treinadores.IniciarPagamentoPlano;

public record IniciarPagamentoPlanoCommand(Guid TreinadorId, MetodoPagamento Metodo = MetodoPagamento.Pix);
