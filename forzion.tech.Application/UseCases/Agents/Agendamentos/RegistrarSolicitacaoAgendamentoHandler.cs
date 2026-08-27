using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Services;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Application.UseCases.Agents.Agendamentos;

public class RegistrarSolicitacaoAgendamentoHandler(
    ITreinadorRepository treinadorRepository,
    IPacoteRepository pacoteRepository,
    IBloqueioAgendaRepository bloqueioAgendaRepository,
    ISolicitacaoAgendamentoRepository solicitacaoAgendamentoRepository,
    ResolvedorLeadAgendamento resolvedorLeadAgendamento,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IDatabaseErrorInspector databaseErrorInspector)
{
    public virtual Task<Result<StagedBookingRequest>> HandleAsync(RegistrarSolicitacaoAgendamentoCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<StagedBookingRequest>> HandleAsyncCore(RegistrarSolicitacaoAgendamentoCommand command, CancellationToken cancellationToken)
    {
        if (!command.ConsentGranted)
            return Result.Failure<StagedBookingRequest>(SolicitacaoAgendamentoAgenteErrors.ConsentimentoNaoConcedido);

        if (string.IsNullOrWhiteSpace(command.Name))
            return Result.Failure<StagedBookingRequest>(LeadErrors.NomeObrigatorio);

        var nomeNormalizado = command.Name.Trim();
        if (nomeNormalizado.Length > 200)
            return Result.Failure<StagedBookingRequest>(LeadErrors.NomeMuitoLongo);

        if (string.IsNullOrWhiteSpace(command.SlotId))
            return Result.Failure<StagedBookingRequest>(SolicitacaoAgendamentoErrors.SlotIdObrigatorio);

        if (string.IsNullOrWhiteSpace(command.IdempotencyKey))
            return Result.Failure<StagedBookingRequest>(SolicitacaoAgendamentoErrors.IdempotencyKeyObrigatoria);
        if (command.IdempotencyKey.Length > 200)
            return Result.Failure<StagedBookingRequest>(SolicitacaoAgendamentoErrors.IdempotencyKeyMuitoLonga);

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        var consentimentoResult = ConsentimentoLead.Criar(command.ConsentPurpose, command.ConsentGrantedAt ?? agora, agora);
        if (consentimentoResult.IsFailure)
            return Result.Failure<StagedBookingRequest>(consentimentoResult.Error!);

        var origemResult = OrigemLead.Criar(command.OriginUserAgent, command.OriginAssistant);
        if (origemResult.IsFailure)
            return Result.Failure<StagedBookingRequest>(origemResult.Error!);

        var tipoContatoResult = ParseTipoContato(command.ContactType);
        if (tipoContatoResult.IsFailure)
            return Result.Failure<StagedBookingRequest>(tipoContatoResult.Error!);

        var contatoResult = ContatoLead.Criar(tipoContatoResult.Value, command.ContactValue);
        if (contatoResult.IsFailure)
            return Result.Failure<StagedBookingRequest>(contatoResult.Error!);

        var contato = contatoResult.Value;
        var consentimento = consentimentoResult.Value;

        var treinador = await treinadorRepository.ObterPorIdAsync(command.TenantId, cancellationToken).ConfigureAwait(false);
        if (!AgentTenantGuard.EstaPublicado(treinador))
            return Result.Failure<StagedBookingRequest>(TreinadorErrors.NaoEncontrado);

        var pacote = await pacoteRepository.ObterPorIdAsync(command.ServiceId, cancellationToken).ConfigureAwait(false);
        if (pacote is null || pacote.TreinadorId != command.TenantId || !pacote.IsPublico || !pacote.IsAtivo || pacote.DuracaoMinutos is not { } duracaoMinutos)
            return Result.Failure<StagedBookingRequest>(PacoteErrors.NaoEncontrado);

        var argumentosHash = IdempotenciaAgendamento.Calcular(command.ServiceId, command.SlotId, nomeNormalizado, contato.Tipo, contato.Valor, consentimento.Finalidade);

        var existente = await solicitacaoAgendamentoRepository
            .ObterPorIdempotencyKeyAsync(command.TenantId, command.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (existente is not null)
            return ResolverIdempotente(existente, argumentosHash);

        // Sem try/catch: se o fuso persistido não resolver, a exceção sobe para o
        // AgentExceptionFilter e vira 503 — mesmo precedente de ConsultarDisponibilidadeAgenteHandler.
        var fuso = TimeZoneInfo.FindSystemTimeZoneById(treinador.FusoHorario);
        var horizonteUtc = agora.AddDays(treinador.PoliticaAgenda.HorizonteDias);

        var bloqueios = await bloqueioAgendaRepository
            .ListarVigentesAsync(command.TenantId, agora, horizonteUtc, cancellationToken)
            .ConfigureAwait(false);

        var parametros = new ParametrosDerivacao(
            command.TenantId,
            command.ServiceId,
            duracaoMinutos,
            agora,
            horizonteUtc,
            agora,
            fuso,
            treinador.PoliticaAgenda,
            treinador.PerfilPublico.HorariosFuncionamento,
            bloqueios);

        var slot = DerivadorDisponibilidade.LocalizarPorId(parametros, command.SlotId);
        if (slot is null)
            return Result.Failure<StagedBookingRequest>(SolicitacaoAgendamentoAgenteErrors.SlotNaoEncontrado);

        var confirmadas = await solicitacaoAgendamentoRepository
            .ContarConfirmadasSobrepostasAsync(command.TenantId, slot.InicioUtc, slot.FimUtc, cancellationToken)
            .ConfigureAwait(false);
        if (confirmadas >= pacote.CapacidadeMaxima)
            return Result.Failure<StagedBookingRequest>(SolicitacaoAgendamentoAgenteErrors.SlotIndisponivel);

        var leadResult = await resolvedorLeadAgendamento
            .ResolverAsync(command.TenantId, nomeNormalizado, contato, consentimento, origemResult.Value, slot.InicioUtc, agora, cancellationToken)
            .ConfigureAwait(false);
        if (leadResult.IsFailure)
            return Result.Failure<StagedBookingRequest>(leadResult.Error!);

        var solicitacaoResult = SolicitacaoAgendamento.Criar(
            command.TenantId, command.ServiceId, leadResult.Value.Id, slot.SlotId, slot.InicioUtc, slot.FimUtc,
            command.IdempotencyKey, argumentosHash, agora);
        if (solicitacaoResult.IsFailure)
            return Result.Failure<StagedBookingRequest>(solicitacaoResult.Error!);

        var solicitacao = solicitacaoResult.Value;
        await solicitacaoAgendamentoRepository.AdicionarAsync(solicitacao, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        // Corrida: duas requisições com a mesma chave passam o lookup acima e colidem no índice
        // único — a violação significa que o concorrente já gravou primeiro; relê e resolve pelo
        // mesmo critério do caminho normal (precedente RegistrarLeadAgenteHandler).
        catch (Exception ex) when (databaseErrorInspector.EhViolacaoDeUnicidade(ex))
        {
            var vencedor = await solicitacaoAgendamentoRepository
                .ObterPorIdempotencyKeyAsync(command.TenantId, command.IdempotencyKey, cancellationToken)
                .ConfigureAwait(false);
            if (vencedor is null)
                throw;
            return ResolverIdempotente(vencedor, argumentosHash);
        }

        return Result.Success(ProjetarStagedBookingRequest(solicitacao));
    }

    private static Result<StagedBookingRequest> ResolverIdempotente(SolicitacaoAgendamento existente, string argumentosHash) =>
        existente.ArgumentosHash == argumentosHash
            ? Result.Success(ProjetarStagedBookingRequest(existente))
            : Result.Failure<StagedBookingRequest>(SolicitacaoAgendamentoAgenteErrors.IdempotencyConflito);

    private static Result<TipoContatoLead> ParseTipoContato(string contactType) => contactType switch
    {
        "email" => Result.Success(TipoContatoLead.Email),
        "phone" => Result.Success(TipoContatoLead.Telefone),
        "whatsapp" => Result.Success(TipoContatoLead.WhatsApp),
        _ => Result.Failure<TipoContatoLead>(SolicitacaoAgendamentoAgenteErrors.TipoContatoInvalido)
    };

    private static StagedBookingRequest ProjetarStagedBookingRequest(SolicitacaoAgendamento solicitacao) =>
        new(solicitacao.Id.ToString(), "pending-agent");
}
