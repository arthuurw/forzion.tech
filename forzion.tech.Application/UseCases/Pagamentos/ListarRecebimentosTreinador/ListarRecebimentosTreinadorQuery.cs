namespace forzion.tech.Application.UseCases.Pagamentos.ListarRecebimentosTreinador;

public record ListarRecebimentosTreinadorQuery(Guid TreinadorId, string? Cursor, int Tamanho);
