namespace forzion.tech.Domain.Shared.Errors;

public static class HorarioFuncionamentoErrors
{
    public static Error DiaSemanaInvalido => Error.Validation("horario_funcionamento.dia_semana_invalido", "O dia da semana deve estar entre 0 (domingo) e 6 (sábado).");
    public static Error IntervaloInvalido => Error.Validation("horario_funcionamento.intervalo_invalido", "O horário de abertura deve ser anterior ao horário de fechamento.");
}
