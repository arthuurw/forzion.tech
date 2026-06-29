using ClosedXML.Excel;
using FluentAssertions;
using forzion.tech.Application.UseCases.Conta.Lgpd;
using forzion.tech.Infrastructure.Services;

namespace forzion.tech.Tests.Infrastructure.Services;

public class DadosPessoaisExcelRendererTests
{
    private static readonly DateTime Agora = new(2026, 5, 24, 12, 0, 0, DateTimeKind.Utc);

    private static readonly DadosPessoaisExcelRenderer Renderer = new();

    // --- helpers ---

    private static ContaExportDto ContaAluno() => new(
        Guid.NewGuid(), "aluno@test.com", "Aluno", true, Agora, Agora);

    private static ContaExportDto ContaTreinador() => new(
        Guid.NewGuid(), "treinador@test.com", "Treinador", true, Agora, Agora);

    private static AlunoExportDto AlunoDto() => new(
        Guid.NewGuid(), "João Aluno", "aluno@test.com", "+5511999990000",
        "Ativo", 3, "60", "Hipertrofia", "Membros superiores",
        "Intermediario", null, null, null, Agora);

    private static TreinadorExportDto TreinadorDto() => new(
        Guid.NewGuid(), "Coach Pro", "+5521988880000", "Aprovado", Agora, Agora);

    private static DadosPessoaisExport ExportAluno(
        IReadOnlyList<VinculoExportDto>? vinculos = null,
        IReadOnlyList<AssinaturaExportDto>? assinaturas = null,
        IReadOnlyList<PagamentoExportDto>? pagamentos = null,
        IReadOnlyList<PacoteExportDto>? pacotes = null,
        IReadOnlyList<TreinoExportDto>? treinos = null,
        IReadOnlyList<ExecucaoExportDto>? execucoes = null,
        IReadOnlyList<EmailDeliveryLogExportDto>? emailLogs = null,
        IReadOnlyList<WhatsAppDeliveryLogExportDto>? waLogs = null) =>
        new("1.0", Agora, ContaAluno(), AlunoDto(), null,
            vinculos ?? [],
            assinaturas ?? [],
            pagamentos ?? [],
            pacotes ?? [],
            treinos ?? [],
            execucoes ?? [],
            emailLogs ?? [],
            waLogs ?? []);

    private static DadosPessoaisExport ExportTreinador(
        IReadOnlyList<TreinoExportDto>? treinos = null) =>
        new("1.0", Agora, ContaTreinador(), null, TreinadorDto(),
            [], [], [], [],
            treinos ?? [],
            [], [], []);

    private static XLWorkbook WorkbookFromBytes(byte[] bytes)
    {
        var ms = new MemoryStream(bytes);
        return new XLWorkbook(ms);
    }

    // --- tab count ---

    [Fact]
    public void Render_Aluno_GeraExatamente10Abas()
    {
        var bytes = Renderer.Render(ExportAluno());

        using var wb = WorkbookFromBytes(bytes);
        wb.Worksheets.Should().HaveCount(10);
    }

    [Fact]
    public void Render_Treinador_GeraExatamente10Abas()
    {
        var bytes = Renderer.Render(ExportTreinador());

        using var wb = WorkbookFromBytes(bytes);
        wb.Worksheets.Should().HaveCount(10);
    }

    // --- tab names & order ---

    [Fact]
    public void Render_NomesEOrdemDasAbas_ConformesEspec()
    {
        var bytes = Renderer.Render(ExportAluno());

        using var wb = WorkbookFromBytes(bytes);
        var names = wb.Worksheets.Select(w => w.Name).ToList();
        names.Should().ContainInOrder(
            "Conta", "Perfil", "Vínculos", "Assinaturas", "Pagamentos",
            "Pacotes", "Treinos", "Execuções", "Logs E-mail", "Logs WhatsApp");
    }

    // --- row counts match item counts ---

    [Fact]
    public void Render_AbaTreinos_TemHeaderMaisUmaLinhaPorItem()
    {
        var treinos = new List<TreinoExportDto>
        {
            new(Guid.NewGuid(), "Treino A", "Forca", "Avancado", Agora),
            new(Guid.NewGuid(), "Treino B", "Resistencia", "Basico", Agora),
        };
        var bytes = Renderer.Render(ExportAluno(treinos: treinos));

        using var wb = WorkbookFromBytes(bytes);
        var ws = wb.Worksheet("Treinos");
        ws.LastRowUsed()!.RowNumber().Should().Be(treinos.Count + 1);
    }

    [Fact]
    public void Render_AbaPagamentos_TemHeaderMaisUmaLinhaPorItem()
    {
        var pagamentos = new List<PagamentoExportDto>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), 149.90m, "Pago", "Cartao", Agora, Agora),
            new(Guid.NewGuid(), Guid.NewGuid(), 99.00m, "Pendente", "Pix", null, Agora),
            new(Guid.NewGuid(), Guid.NewGuid(), 200.00m, "Pago", "Pix", Agora, Agora),
        };
        var bytes = Renderer.Render(ExportAluno(pagamentos: pagamentos));

        using var wb = WorkbookFromBytes(bytes);
        var ws = wb.Worksheet("Pagamentos");
        ws.LastRowUsed()!.RowNumber().Should().Be(pagamentos.Count + 1);
    }

    // --- Perfil tab changes by account type ---

    [Fact]
    public void Render_PerfilAluno_ContemColunaNomedoAluno()
    {
        var bytes = Renderer.Render(ExportAluno());

        using var wb = WorkbookFromBytes(bytes);
        var ws = wb.Worksheet("Perfil");
        var headers = Enumerable.Range(1, ws.LastColumnUsed()!.ColumnNumber())
            .Select(c => ws.Cell(1, c).GetString())
            .ToList();
        headers.Should().Contain("Nome");
        headers.Should().Contain("AlunoId");
        headers.Should().Contain("DiasDisponiveis");
        headers.Should().Contain("ObservacoesAdicionais");
    }

    [Fact]
    public void Render_PerfilTreinador_ContemColunaNomedoTreinador()
    {
        var bytes = Renderer.Render(ExportTreinador());

        using var wb = WorkbookFromBytes(bytes);
        var ws = wb.Worksheet("Perfil");
        var headers = Enumerable.Range(1, ws.LastColumnUsed()!.ColumnNumber())
            .Select(c => ws.Cell(1, c).GetString())
            .ToList();
        headers.Should().Contain("Nome");
        headers.Should().Contain("TreinadorId");
        headers.Should().Contain("AprovadoEm");
        headers.Should().NotContain("AlunoId");
    }

    // --- empty list tabs still have header row ---

    [Fact]
    public void Render_AbaVazia_AindaTemLinhaDeHeader()
    {
        var bytes = Renderer.Render(ExportAluno());

        using var wb = WorkbookFromBytes(bytes);
        var tabsWithLists = new[] { "Vínculos", "Assinaturas", "Pagamentos", "Pacotes", "Treinos", "Execuções", "Logs E-mail", "Logs WhatsApp" };
        foreach (var name in tabsWithLists)
        {
            var ws = wb.Worksheet(name);
            ws.LastRowUsed().Should().NotBeNull(because: $"aba '{name}' deve ter ao menos o header");
            ws.LastRowUsed()!.RowNumber().Should().Be(1, because: $"aba '{name}' vazia deve ter apenas 1 linha (header)");
        }
    }

    // --- monetary values are numeric ---

    [Fact]
    public void Render_ValorAssinatura_EhNumerico()
    {
        var assinaturas = new List<AssinaturaExportDto>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                299.90m, "Ativa", Agora, Agora.AddMonths(1), null, Agora),
        };
        var bytes = Renderer.Render(ExportAluno(assinaturas: assinaturas));

        using var wb = WorkbookFromBytes(bytes);
        var ws = wb.Worksheet("Assinaturas");
        // Valor is column 4; numeric cell type means GetValue<double> won't throw
        var valor = ws.Cell(2, 4).GetDouble();
        valor.Should().BeApproximately(299.90, 0.001);
    }
}
