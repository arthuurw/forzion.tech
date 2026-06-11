using ClosedXML.Excel;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.UseCases.Conta.Lgpd;

namespace forzion.tech.Infrastructure.Services;

public class DadosPessoaisExcelRenderer : IDadosPessoaisExcelRenderer
{
    private const string DateTimeFormat = "dd/MM/yyyy HH:mm";
    private const string DateFormat = "dd/MM/yyyy";

    public byte[] Render(DadosPessoaisExport dados)
    {
        using var wb = new XLWorkbook();

        AddConta(wb, dados.Conta);
        AddPerfil(wb, dados.Aluno, dados.Treinador);
        AddVinculos(wb, dados.Vinculos);
        AddAssinaturas(wb, dados.Assinaturas);
        AddPagamentos(wb, dados.Pagamentos);
        AddPacotes(wb, dados.Pacotes);
        AddTreinos(wb, dados.Treinos);
        AddExecucoes(wb, dados.Execucoes);
        AddEmailLogs(wb, dados.EmailDeliveryLogs);
        AddWhatsAppLogs(wb, dados.WhatsAppDeliveryLogs);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void AddConta(XLWorkbook wb, ContaExportDto conta)
    {
        var ws = wb.Worksheets.Add("Conta");
        ws.Cell(1, 1).Value = "ContaId";
        ws.Cell(1, 2).Value = "Email";
        ws.Cell(1, 3).Value = "TipoConta";
        ws.Cell(1, 4).Value = "EmailVerificado";
        ws.Cell(1, 5).Value = "VerificadoEm";
        ws.Cell(1, 6).Value = "CreatedAt";

        ws.Cell(2, 1).Value = conta.ContaId.ToString();
        ws.Cell(2, 2).Value = conta.Email;
        ws.Cell(2, 3).Value = conta.TipoConta;
        ws.Cell(2, 4).Value = conta.EmailVerificado;
        ws.Cell(2, 5).Value = conta.VerificadoEm.HasValue
            ? conta.VerificadoEm.Value.ToString(DateTimeFormat)
            : string.Empty;
        ws.Cell(2, 6).Value = conta.CreatedAt.ToString(DateTimeFormat);
    }

    private static void AddPerfil(XLWorkbook wb, AlunoExportDto? aluno, TreinadorExportDto? treinador)
    {
        var ws = wb.Worksheets.Add("Perfil");

        if (aluno is not null)
        {
            ws.Cell(1, 1).Value = "AlunoId";
            ws.Cell(1, 2).Value = "Nome";
            ws.Cell(1, 3).Value = "Email";
            ws.Cell(1, 4).Value = "Telefone";
            ws.Cell(1, 5).Value = "Status";
            ws.Cell(1, 6).Value = "DiasDisponiveis";
            ws.Cell(1, 7).Value = "TempoDisponivelMinutos";
            ws.Cell(1, 8).Value = "Finalidade";
            ws.Cell(1, 9).Value = "FocoTreino";
            ws.Cell(1, 10).Value = "NivelCondicionamento";
            ws.Cell(1, 11).Value = "LimitacoesFisicas";
            ws.Cell(1, 12).Value = "Doencas";
            ws.Cell(1, 13).Value = "ObservacoesAdicionais";
            ws.Cell(1, 14).Value = "CreatedAt";

            ws.Cell(2, 1).Value = aluno.AlunoId.ToString();
            ws.Cell(2, 2).Value = aluno.Nome;
            ws.Cell(2, 3).Value = aluno.Email ?? string.Empty;
            ws.Cell(2, 4).Value = aluno.Telefone ?? string.Empty;
            ws.Cell(2, 5).Value = aluno.Status;
            if (aluno.DiasDisponiveis.HasValue)
                ws.Cell(2, 6).Value = aluno.DiasDisponiveis.Value;
            else
                ws.Cell(2, 6).Value = string.Empty;
            ws.Cell(2, 7).Value = aluno.TempoDisponivelMinutos ?? string.Empty;
            ws.Cell(2, 8).Value = aluno.Finalidade ?? string.Empty;
            ws.Cell(2, 9).Value = aluno.FocoTreino ?? string.Empty;
            ws.Cell(2, 10).Value = aluno.NivelCondicionamento ?? string.Empty;
            ws.Cell(2, 11).Value = aluno.LimitacoesFisicas ?? string.Empty;
            ws.Cell(2, 12).Value = aluno.Doencas ?? string.Empty;
            ws.Cell(2, 13).Value = aluno.ObservacoesAdicionais ?? string.Empty;
            ws.Cell(2, 14).Value = aluno.CreatedAt.ToString(DateTimeFormat);
        }
        else if (treinador is not null)
        {
            ws.Cell(1, 1).Value = "TreinadorId";
            ws.Cell(1, 2).Value = "Nome";
            ws.Cell(1, 3).Value = "Telefone";
            ws.Cell(1, 4).Value = "Status";
            ws.Cell(1, 5).Value = "AprovadoEm";
            ws.Cell(1, 6).Value = "CreatedAt";

            ws.Cell(2, 1).Value = treinador.TreinadorId.ToString();
            ws.Cell(2, 2).Value = treinador.Nome;
            ws.Cell(2, 3).Value = treinador.Telefone ?? string.Empty;
            ws.Cell(2, 4).Value = treinador.Status;
            ws.Cell(2, 5).Value = treinador.AprovadoEm.HasValue
                ? treinador.AprovadoEm.Value.ToString(DateTimeFormat)
                : string.Empty;
            ws.Cell(2, 6).Value = treinador.CreatedAt.ToString(DateTimeFormat);
        }
        // LGPD Art. 18, IV portability: recipient must be able to validate that all sections were exported;
        // omitting a tab would make it indistinguishable from a truncated export.
    }

    private static void AddVinculos(XLWorkbook wb, IReadOnlyList<VinculoExportDto> vinculos)
    {
        var ws = wb.Worksheets.Add("Vínculos");
        ws.Cell(1, 1).Value = "VinculoId";
        ws.Cell(1, 2).Value = "TreinadorId";
        ws.Cell(1, 3).Value = "AlunoId";
        ws.Cell(1, 4).Value = "Status";
        ws.Cell(1, 5).Value = "DataInicio";
        ws.Cell(1, 6).Value = "DataFim";
        ws.Cell(1, 7).Value = "CreatedAt";

        for (var i = 0; i < vinculos.Count; i++)
        {
            var v = vinculos[i];
            var row = i + 2;
            ws.Cell(row, 1).Value = v.VinculoId.ToString();
            ws.Cell(row, 2).Value = v.TreinadorId.ToString();
            ws.Cell(row, 3).Value = v.AlunoId.ToString();
            ws.Cell(row, 4).Value = v.Status;
            ws.Cell(row, 5).Value = v.DataInicio.HasValue
                ? v.DataInicio.Value.ToString(DateFormat)
                : string.Empty;
            ws.Cell(row, 6).Value = v.DataFim.HasValue
                ? v.DataFim.Value.ToString(DateFormat)
                : string.Empty;
            ws.Cell(row, 7).Value = v.CreatedAt.ToString(DateTimeFormat);
        }
    }

    private static void AddAssinaturas(XLWorkbook wb, IReadOnlyList<AssinaturaExportDto> assinaturas)
    {
        var ws = wb.Worksheets.Add("Assinaturas");
        ws.Cell(1, 1).Value = "AssinaturaId";
        ws.Cell(1, 2).Value = "PacoteId";
        ws.Cell(1, 3).Value = "TreinadorId";
        ws.Cell(1, 4).Value = "Valor";
        ws.Cell(1, 5).Value = "Status";
        ws.Cell(1, 6).Value = "DataInicio";
        ws.Cell(1, 7).Value = "DataProximaCobranca";
        ws.Cell(1, 8).Value = "DataCancelamento";
        ws.Cell(1, 9).Value = "CreatedAt";

        for (var i = 0; i < assinaturas.Count; i++)
        {
            var a = assinaturas[i];
            var row = i + 2;
            ws.Cell(row, 1).Value = a.AssinaturaId.ToString();
            ws.Cell(row, 2).Value = a.PacoteId.ToString();
            ws.Cell(row, 3).Value = a.TreinadorId.ToString();
            ws.Cell(row, 4).Value = a.Valor;
            ws.Cell(row, 5).Value = a.Status;
            ws.Cell(row, 6).Value = a.DataInicio.ToString(DateFormat);
            ws.Cell(row, 7).Value = a.DataProximaCobranca.ToString(DateFormat);
            ws.Cell(row, 8).Value = a.DataCancelamento.HasValue
                ? a.DataCancelamento.Value.ToString(DateFormat)
                : string.Empty;
            ws.Cell(row, 9).Value = a.CreatedAt.ToString(DateTimeFormat);
        }
    }

    private static void AddPagamentos(XLWorkbook wb, IReadOnlyList<PagamentoExportDto> pagamentos)
    {
        var ws = wb.Worksheets.Add("Pagamentos");
        ws.Cell(1, 1).Value = "PagamentoId";
        ws.Cell(1, 2).Value = "AssinaturaId";
        ws.Cell(1, 3).Value = "Valor";
        ws.Cell(1, 4).Value = "Status";
        ws.Cell(1, 5).Value = "MetodoPagamento";
        ws.Cell(1, 6).Value = "DataPagamento";
        ws.Cell(1, 7).Value = "CreatedAt";

        for (var i = 0; i < pagamentos.Count; i++)
        {
            var p = pagamentos[i];
            var row = i + 2;
            ws.Cell(row, 1).Value = p.PagamentoId.ToString();
            ws.Cell(row, 2).Value = p.AssinaturaId.ToString();
            ws.Cell(row, 3).Value = p.Valor;
            ws.Cell(row, 4).Value = p.Status;
            ws.Cell(row, 5).Value = p.MetodoPagamento;
            ws.Cell(row, 6).Value = p.DataPagamento.HasValue
                ? p.DataPagamento.Value.ToString(DateFormat)
                : string.Empty;
            ws.Cell(row, 7).Value = p.CreatedAt.ToString(DateTimeFormat);
        }
    }

    private static void AddPacotes(XLWorkbook wb, IReadOnlyList<PacoteExportDto> pacotes)
    {
        var ws = wb.Worksheets.Add("Pacotes");
        ws.Cell(1, 1).Value = "PacoteId";
        ws.Cell(1, 2).Value = "Nome";
        ws.Cell(1, 3).Value = "Preco";
        ws.Cell(1, 4).Value = "Descricao";
        ws.Cell(1, 5).Value = "IsAtivo";
        ws.Cell(1, 6).Value = "CreatedAt";

        for (var i = 0; i < pacotes.Count; i++)
        {
            var p = pacotes[i];
            var row = i + 2;
            ws.Cell(row, 1).Value = p.PacoteId.ToString();
            ws.Cell(row, 2).Value = p.Nome;
            ws.Cell(row, 3).Value = p.Preco;
            ws.Cell(row, 4).Value = p.Descricao ?? string.Empty;
            ws.Cell(row, 5).Value = p.IsAtivo;
            ws.Cell(row, 6).Value = p.CreatedAt.ToString(DateTimeFormat);
        }
    }

    private static void AddTreinos(XLWorkbook wb, IReadOnlyList<TreinoExportDto> treinos)
    {
        var ws = wb.Worksheets.Add("Treinos");
        ws.Cell(1, 1).Value = "TreinoId";
        ws.Cell(1, 2).Value = "Nome";
        ws.Cell(1, 3).Value = "Objetivo";
        ws.Cell(1, 4).Value = "Dificuldade";
        ws.Cell(1, 5).Value = "CreatedAt";

        for (var i = 0; i < treinos.Count; i++)
        {
            var t = treinos[i];
            var row = i + 2;
            ws.Cell(row, 1).Value = t.TreinoId.ToString();
            ws.Cell(row, 2).Value = t.Nome;
            ws.Cell(row, 3).Value = t.Objetivo;
            ws.Cell(row, 4).Value = t.Dificuldade;
            ws.Cell(row, 5).Value = t.CreatedAt.ToString(DateTimeFormat);
        }
    }

    private static void AddExecucoes(XLWorkbook wb, IReadOnlyList<ExecucaoExportDto> execucoes)
    {
        var ws = wb.Worksheets.Add("Execuções");
        ws.Cell(1, 1).Value = "ExecucaoId";
        ws.Cell(1, 2).Value = "TreinoId";
        ws.Cell(1, 3).Value = "DataExecucao";
        ws.Cell(1, 4).Value = "Observacao";
        ws.Cell(1, 5).Value = "CreatedAt";

        for (var i = 0; i < execucoes.Count; i++)
        {
            var e = execucoes[i];
            var row = i + 2;
            ws.Cell(row, 1).Value = e.ExecucaoId.ToString();
            ws.Cell(row, 2).Value = e.TreinoId.ToString();
            ws.Cell(row, 3).Value = e.DataExecucao.ToString(DateFormat);
            ws.Cell(row, 4).Value = e.Observacao ?? string.Empty;
            ws.Cell(row, 5).Value = e.CreatedAt.ToString(DateTimeFormat);
        }
    }

    private static void AddEmailLogs(XLWorkbook wb, IReadOnlyList<EmailDeliveryLogExportDto> logs)
    {
        var ws = wb.Worksheets.Add("Logs E-mail");
        ws.Cell(1, 1).Value = "LogId";
        ws.Cell(1, 2).Value = "EventType";
        ws.Cell(1, 3).Value = "OcorridoEm";

        for (var i = 0; i < logs.Count; i++)
        {
            var l = logs[i];
            var row = i + 2;
            ws.Cell(row, 1).Value = l.LogId.ToString();
            ws.Cell(row, 2).Value = l.EventType;
            ws.Cell(row, 3).Value = l.OcorridoEm.ToString(DateTimeFormat);
        }
    }

    private static void AddWhatsAppLogs(XLWorkbook wb, IReadOnlyList<WhatsAppDeliveryLogExportDto> logs)
    {
        var ws = wb.Worksheets.Add("Logs WhatsApp");
        ws.Cell(1, 1).Value = "LogId";
        ws.Cell(1, 2).Value = "EventType";
        ws.Cell(1, 3).Value = "OcorridoEm";

        for (var i = 0; i < logs.Count; i++)
        {
            var l = logs[i];
            var row = i + 2;
            ws.Cell(row, 1).Value = l.LogId.ToString();
            ws.Cell(row, 2).Value = l.EventType;
            ws.Cell(row, 3).Value = l.OcorridoEm.ToString(DateTimeFormat);
        }
    }
}
