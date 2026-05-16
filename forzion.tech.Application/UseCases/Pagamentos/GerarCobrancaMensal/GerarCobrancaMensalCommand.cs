using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Pagamentos.GerarCobrancaMensal;

public record GerarCobrancaMensalCommand(
    Guid AssinaturaId,
    Guid TreinadorId,
    MetodoPagamento Metodo = MetodoPagamento.Pix);
