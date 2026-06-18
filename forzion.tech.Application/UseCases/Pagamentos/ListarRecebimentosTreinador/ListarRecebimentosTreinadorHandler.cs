using System.Globalization;
using System.Text;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Settings;
using forzion.tech.Application.UseCases.Pagamentos;
using forzion.tech.Domain.Enums;
using Microsoft.Extensions.Options;

namespace forzion.tech.Application.UseCases.Pagamentos.ListarRecebimentosTreinador;

public record RecebimentoTreinadorResponse(
    Guid PagamentoId,
    decimal Bruto,
    decimal? TaxaPercent,
    decimal? LiquidoEstimado,
    string Status,
    string NomeAluno,
    string Metodo,
    DateTime CreatedAt,
    DateTime? DataPagamento);

public record ListarRecebimentosTreinadorResultado(
    IReadOnlyList<RecebimentoTreinadorResponse> Itens,
    string? ProximoCursor,
    decimal TaxaPlataformaPercent);

public class ListarRecebimentosTreinadorHandler(
    IPagamentoRepository pagamentoRepository,
    IOptions<PaymentSettings> paymentSettings)
{
    private const int TamanhoMaximo = 50;
    private const int TamanhoPadrao = 20;
    private static readonly HashSet<PagamentoStatus> SemRecebimento = [PagamentoStatus.Falhou, PagamentoStatus.Expirado];
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

        var resposta = itens.Select(MapearItem).ToList();
        return new ListarRecebimentosTreinadorResultado(resposta, proximoCursor, _taxaPlataformaPercent);
    }

    private RecebimentoTreinadorResponse MapearItem(RecebimentoTreinadorItem i)
    {
        decimal? taxa = null;
        decimal? liquido = null;
        if (!SemRecebimento.Contains(i.Status))
        {
            var (valorCentavos, taxaCentavos) = MoneyCentavos.ValorETaxaCentavos(i.Valor, _taxaPlataformaPercent);
            taxa = _taxaPlataformaPercent;
            liquido = (valorCentavos - taxaCentavos) / 100m;
        }

        return new RecebimentoTreinadorResponse(
            i.PagamentoId, i.Valor, taxa, liquido,
            i.Status.ToString(), i.NomeAluno, i.Metodo.ToString(), i.CreatedAt, i.DataPagamento);
    }

    private static string CodificarCursor(DateTime createdAt, Guid id)
    {
        var bytes = Encoding.UTF8.GetBytes($"{createdAt.Ticks.ToString(CultureInfo.InvariantCulture)}:{id}");
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static (DateTime?, Guid?) DecodificarCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
            return (null, null);

        try
        {
            var normalizado = cursor.Replace('-', '+').Replace('_', '/');
            var padding = (4 - normalizado.Length % 4) % 4;
            var texto = Encoding.UTF8.GetString(
                Convert.FromBase64String(normalizado.PadRight(normalizado.Length + padding, '=')));
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
