using System.Globalization;
using System.Text;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using Microsoft.Extensions.Options;

namespace forzion.tech.Application.UseCases.Pagamentos.ListarRecebimentosTreinador;

public record RecebimentoTreinadorResponse(
    Guid PagamentoId,
    decimal Bruto,
    decimal TaxaPercent,
    decimal LiquidoEstimado,
    string Status,
    string NomeAluno,
    string Metodo,
    DateTime CreatedAt,
    DateTime? DataPagamento);

public record ListarRecebimentosTreinadorResultado(
    IReadOnlyList<RecebimentoTreinadorResponse> Itens,
    string? ProximoCursor);

public class ListarRecebimentosTreinadorHandler(
    IPagamentoRepository pagamentoRepository,
    IOptions<PaymentSettings> paymentSettings)
{
    private const int TamanhoMaximo = 50;
    private const int TamanhoPadrao = 20;
    private readonly decimal _taxaPlataformaPercent = paymentSettings.Value.TaxaPlataformaPercent;

    public virtual async Task<ListarRecebimentosTreinadorResultado> HandleAsync(
        ListarRecebimentosTreinadorQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var tamanho = query.Tamanho is < 1 or > TamanhoMaximo ? TamanhoPadrao : query.Tamanho;
        var (cursorCreatedAt, cursorId) = DecodificarCursor(query.Cursor);

        var itens = await pagamentoRepository.ListarPorTreinadorAsync(
            query.TreinadorId, cursorCreatedAt, cursorId, tamanho + 1, cancellationToken).ConfigureAwait(false);

        string? proximoCursor = null;
        if (itens.Count > tamanho)
        {
            var ultimo = itens[tamanho - 1];
            proximoCursor = CodificarCursor(ultimo.CreatedAt, ultimo.PagamentoId);
            itens = itens.Take(tamanho).ToList();
        }

        var resposta = itens.Select(i => new RecebimentoTreinadorResponse(
            i.PagamentoId,
            i.Valor,
            _taxaPlataformaPercent,
            Math.Round(i.Valor * (1 - _taxaPlataformaPercent / 100m), 2, MidpointRounding.ToZero),
            i.Status.ToString(),
            i.NomeAluno,
            i.Metodo.ToString(),
            i.CreatedAt,
            i.DataPagamento)).ToList();

        return new ListarRecebimentosTreinadorResultado(resposta, proximoCursor);
    }

    private static string CodificarCursor(DateTime createdAt, Guid id) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(
            $"{createdAt.Ticks.ToString(CultureInfo.InvariantCulture)}:{id}"));

    private static (DateTime?, Guid?) DecodificarCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
            return (null, null);

        try
        {
            var texto = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var sep = texto.IndexOf(':', StringComparison.Ordinal);
            if (sep <= 0)
                return (null, null);

            var ticks = long.Parse(texto[..sep], CultureInfo.InvariantCulture);
            var id = Guid.Parse(texto[(sep + 1)..]);
            return (new DateTime(ticks, DateTimeKind.Utc), id);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            return (null, null);
        }
    }
}
