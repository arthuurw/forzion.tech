namespace forzion.tech.Domain.Shared.Errors;

public static class BloqueioAgendaErrors
{
    public static Error TreinadorIdInvalido => Error.Validation("bloqueio_agenda.treinador_id_invalido", "O identificador do treinador é inválido.");
    public static Error IntervaloPontualInvalido => Error.Validation("bloqueio_agenda.intervalo_pontual_invalido", "O início do bloqueio deve ser anterior ao fim.");
    public static Error DiaSemanaInvalido => Error.Validation("bloqueio_agenda.dia_semana_invalido", "O dia da semana deve estar entre 0 (domingo) e 6 (sábado).");
    public static Error IntervaloRecorrenteInvalido => Error.Validation("bloqueio_agenda.intervalo_recorrente_invalido", "O horário de início deve ser anterior ao horário de fim.");
    public static Error MotivoMuitoLongo => Error.Validation("bloqueio_agenda.motivo_muito_longo", "O motivo deve ter no máximo 200 caracteres.");
    public static Error NaoEncontrado => Error.NotFound("bloqueio_agenda.nao_encontrado", "Bloqueio de agenda não encontrado.");
}
