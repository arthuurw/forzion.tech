using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Application.UseCases.Conta.Lgpd;

public record ContaExportDto(
    Guid ContaId,
    string Email,
    string TipoConta,
    bool EmailVerificado,
    DateTime? VerificadoEm,
    DateTime CreatedAt);

public record AlunoExportDto(
    Guid AlunoId,
    string Nome,
    string? Email,
    string? Telefone,
    string Status,
    int? DiasDisponiveis,
    string? TempoDisponivelMinutos,
    string? Finalidade,
    string? FocoTreino,
    string? NivelCondicionamento,
    string? LimitacoesFisicas,
    string? Doencas,
    string? ObservacoesAdicionais,
    DateTime CreatedAt);

public record TreinadorExportDto(
    Guid TreinadorId,
    string Nome,
    string? Telefone,
    string Status,
    DateTime? AprovadoEm,
    DateTime CreatedAt);

public record VinculoExportDto(
    Guid VinculoId,
    Guid TreinadorId,
    Guid AlunoId,
    string Status,
    DateTime? DataInicio,
    DateTime? DataFim,
    DateTime CreatedAt);

public record AssinaturaExportDto(
    Guid AssinaturaId,
    Guid PacoteId,
    Guid TreinadorId,
    decimal Valor,
    string Status,
    DateTime DataInicio,
    DateTime DataProximaCobranca,
    DateTime? DataCancelamento,
    DateTime CreatedAt);

public record PagamentoExportDto(
    Guid PagamentoId,
    Guid AssinaturaId,
    decimal Valor,
    string Status,
    string MetodoPagamento,
    DateTime? DataPagamento,
    DateTime CreatedAt);

public record PacoteExportDto(
    Guid PacoteId,
    string Nome,
    decimal Preco,
    string? Descricao,
    bool IsAtivo,
    DateTime CreatedAt);

public record TreinoExportDto(
    Guid TreinoId,
    string Nome,
    string Objetivo,
    string Dificuldade,
    DateTime CreatedAt);

public record ExecucaoExportDto(
    Guid ExecucaoId,
    Guid TreinoId,
    DateTime DataExecucao,
    string? Observacao,
    DateTime CreatedAt);

public record EmailDeliveryLogExportDto(
    Guid LogId,
    string EventType,
    DateTime OcorridoEm);

public record WhatsAppDeliveryLogExportDto(
    Guid LogId,
    string EventType,
    DateTime OcorridoEm);

/// <summary>
/// Versioned portability export (LGPD Art. 18, IV).
/// Contains only the titular's data — never third-party PII.
/// Refresh token hashes são DELIBERADAMENTE omitidos: são credenciais de segurança
/// (SHA-256, não reversíveis a dado pessoal portável), não objeto de portabilidade (SEC-4).
/// </summary>
public record DadosPessoaisExport(
    string Versao,
    DateTime GeradoEm,
    ContaExportDto Conta,
    AlunoExportDto? Aluno,
    TreinadorExportDto? Treinador,
    IReadOnlyList<VinculoExportDto> Vinculos,
    IReadOnlyList<AssinaturaExportDto> Assinaturas,
    IReadOnlyList<PagamentoExportDto> Pagamentos,
    IReadOnlyList<PacoteExportDto> Pacotes,
    IReadOnlyList<TreinoExportDto> Treinos,
    IReadOnlyList<ExecucaoExportDto> Execucoes,
    IReadOnlyList<EmailDeliveryLogExportDto> EmailDeliveryLogs,
    IReadOnlyList<WhatsAppDeliveryLogExportDto> WhatsAppDeliveryLogs);

public record ExportarDadosPessoaisCommand(Guid ContaId);

public class ExportarDadosPessoaisHandler(
    IContaRepository contaRepository,
    IAlunoRepository alunoRepository,
    ITreinadorRepository treinadorRepository,
    IVinculoTreinadorAlunoRepository vinculoRepository,
    IAssinaturaAlunoRepository assinaturaAlunoRepository,
    IPagamentoRepository pagamentoRepository,
    IPacoteRepository pacoteRepository,
    ITreinoRepository treinoRepository,
    IExecucaoTreinoRepository execucaoTreinoRepository,
    IEmailDeliveryLogRepository emailDeliveryLogRepository,
    IWhatsAppDeliveryLogRepository whatsAppDeliveryLogRepository,
    ILogAprovacaoRepository logAprovacaoRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public virtual Task<Result<DadosPessoaisExport>> HandleAsync(
        ExportarDadosPessoaisCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<DadosPessoaisExport>> HandleAsyncCore(
        ExportarDadosPessoaisCommand command,
        CancellationToken cancellationToken)
    {
        var agora = timeProvider.GetUtcNow().UtcDateTime;

        var conta = await contaRepository
            .ObterPorIdAsync(command.ContaId, cancellationToken).ConfigureAwait(false);
        if (conta is null)
            return Result.Failure<DadosPessoaisExport>(
                Error.NotFound("conta.nao_encontrada", "Conta não encontrada."));

        var contaDto = new ContaExportDto(
            conta.Id,
            conta.Email.Value,
            conta.TipoConta.ToString(),
            conta.EmailVerificado,
            conta.VerificadoEm,
            conta.CreatedAt);

        AlunoExportDto? alunoDto = null;
        TreinadorExportDto? treinadorDto = null;

        if (conta.TipoConta == TipoConta.Aluno)
        {
            var aluno = await alunoRepository
                .ObterPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false);
            if (aluno is not null)
                alunoDto = MapAluno(aluno);
        }
        else if (conta.TipoConta == TipoConta.Treinador)
        {
            var treinador = await treinadorRepository
                .ObterPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false);
            if (treinador is not null)
                treinadorDto = MapTreinador(treinador);
        }

        var vinculos = new List<VinculoExportDto>();
        var assinaturas = new List<AssinaturaExportDto>();
        var pagamentos = new List<PagamentoExportDto>();
        var pacotes = new List<PacoteExportDto>();
        var treinos = new List<TreinoExportDto>();
        var execucoes = new List<ExecucaoExportDto>();

        if (conta.TipoConta == TipoConta.Aluno && alunoDto is not null)
        {
            var alunoId = alunoDto.AlunoId;

            // All bonds regardless of status — a pending bond with no subscription must appear.
            var todosVinculos = await vinculoRepository
                .ListarTodosPorAlunoAsync(alunoId, cancellationToken).ConfigureAwait(false);
            vinculos.AddRange(todosVinculos.Select(MapVinculo));

            var assinaturasAluno = await assinaturaAlunoRepository
                .ListarPorAlunoAsync(alunoId, cancellationToken).ConfigureAwait(false);

            foreach (var a in assinaturasAluno)
            {
                assinaturas.Add(MapAssinatura(a));

                var pags = await pagamentoRepository
                    .ListarPorAssinaturaAlunoAsync(a.Id, cancellationToken).ConfigureAwait(false);
                pagamentos.AddRange(pags.Select(MapPagamento));
            }

            var (treinosItems, _) = await treinoRepository
                .ListarPorAlunoAsync(alunoId, 1, int.MaxValue, cancellationToken).ConfigureAwait(false);
            treinos.AddRange(treinosItems.Select(MapTreino));

            var execItems = await execucaoTreinoRepository
                .ListarPorAlunoAsync(alunoId, 1, int.MaxValue, cancellationToken).ConfigureAwait(false);
            execucoes.AddRange(execItems.Select(MapExecucao));
        }
        else if (conta.TipoConta == TipoConta.Treinador && treinadorDto is not null)
        {
            var treinadorId = treinadorDto.TreinadorId;

            // All bonds regardless of status — Inativo/AguardandoAprovacao must appear.
            var todosVinculos = await vinculoRepository
                .ListarTodosPorTreinadorAsync(treinadorId, cancellationToken).ConfigureAwait(false);
            vinculos.AddRange(todosVinculos.Select(MapVinculo));

            var pacotesTreinador = await pacoteRepository
                .ListarPorTreinadorAsync(treinadorId, cancellationToken).ConfigureAwait(false);
            pacotes.AddRange(pacotesTreinador.Select(MapPacote));

            var (treinosItems, _) = await treinoRepository
                .ListarPorTreinadorAsync(treinadorId, 1, int.MaxValue, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            treinos.AddRange(treinosItems.Select(t => MapTreino(t.Treino)));
        }

        // Delivery logs — only the titular's email/phone (never third-party PII).
        var emailLogs = await emailDeliveryLogRepository
            .ListarPorEmailAsync(conta.Email.Value, cancellationToken).ConfigureAwait(false);

        var whatsAppLogs = new List<WhatsAppDeliveryLogExportDto>();
        var telefone = conta.TipoConta == TipoConta.Aluno
            ? alunoDto?.Telefone
            : treinadorDto?.Telefone;

        if (!string.IsNullOrEmpty(telefone))
        {
            var waLogs = await whatsAppDeliveryLogRepository
                .ListarPorTelefoneAsync(telefone, cancellationToken).ConfigureAwait(false);
            whatsAppLogs.AddRange(waLogs.Select(MapWhatsAppLog));
        }

        // Audit is mandatory — a validation failure means the export must not be returned.
        var logResult = LogAprovacao.Registrar(
            TipoAcaoAprovacao.ExportacaoDados,
            realizadoPorId: command.ContaId,
            entidadeId: command.ContaId,
            entidadeTipo: "Conta",
            agora);
        if (logResult.IsFailure)
            return Result.Failure<DadosPessoaisExport>(logResult.Error!);

        await logAprovacaoRepository
            .AdicionarAsync(logResult.Value, cancellationToken).ConfigureAwait(false);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        var export = new DadosPessoaisExport(
            Versao: "1.0",
            GeradoEm: agora,
            Conta: contaDto,
            Aluno: alunoDto,
            Treinador: treinadorDto,
            Vinculos: vinculos,
            Assinaturas: assinaturas,
            Pagamentos: pagamentos,
            Pacotes: pacotes,
            Treinos: treinos,
            Execucoes: execucoes,
            EmailDeliveryLogs: emailLogs.Select(MapEmailLog).ToList(),
            WhatsAppDeliveryLogs: whatsAppLogs);

        return Result.Success(export);
    }

    private static AlunoExportDto MapAluno(Aluno a) => new(
        a.Id, a.Nome, a.Email?.Value, a.Telefone, a.Status.ToString(),
        a.DiasDisponiveis, a.TempoDisponivelMinutos?.ToString(),
        a.Finalidade?.ToString(), a.FocoTreino, a.NivelCondicionamento?.ToString(),
        a.LimitacoesFisicas, a.Doencas, a.ObservacoesAdicionais, a.CreatedAt);

    private static TreinadorExportDto MapTreinador(Treinador t) => new(
        t.Id, t.Nome, t.Telefone, t.Status.ToString(), t.AprovadoEm, t.CreatedAt);

    private static VinculoExportDto MapVinculo(VinculoTreinadorAluno v) => new(
        v.Id, v.TreinadorId, v.AlunoId, v.Status.ToString(),
        v.DataInicio, v.DataFim, v.CreatedAt);

    private static AssinaturaExportDto MapAssinatura(AssinaturaAluno a) => new(
        a.Id, a.PacoteId, a.TreinadorId, a.Valor, a.Status.ToString(),
        a.DataInicio, a.DataProximaCobranca, a.DataCancelamento, a.CreatedAt);

    private static PagamentoExportDto MapPagamento(Pagamento p) => new(
        p.Id, p.AssinaturaAlunoId, p.Valor, p.Status.ToString(),
        p.MetodoPagamento.ToString(), p.DataPagamento, p.CreatedAt);

    private static PacoteExportDto MapPacote(Pacote p) => new(
        p.Id, p.Nome, p.Preco, p.Descricao, p.IsAtivo, p.CreatedAt);

    private static TreinoExportDto MapTreino(Treino t) => new(
        t.Id, t.Nome, t.Objetivo.ToString(), t.Dificuldade.ToString(), t.CreatedAt);

    private static ExecucaoExportDto MapExecucao(ExecucaoTreino e) => new(
        e.Id, e.TreinoId, e.DataExecucao, e.Observacao, e.CreatedAt);

    private static EmailDeliveryLogExportDto MapEmailLog(EmailDeliveryLog e) => new(
        e.Id, e.EventType, e.OcorridoEm);

    private static WhatsAppDeliveryLogExportDto MapWhatsAppLog(WhatsAppDeliveryLog w) => new(
        w.Id, w.EventType, w.OcorridoEm);
}
