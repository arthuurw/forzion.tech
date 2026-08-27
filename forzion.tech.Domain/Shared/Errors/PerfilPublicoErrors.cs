using forzion.tech.Domain.Services;

namespace forzion.tech.Domain.Shared.Errors;

public static class PerfilPublicoErrors
{
    public static Error NomeFantasiaObrigatorioParaPublicar => Error.Validation("perfil_publico.nome_fantasia_obrigatorio_para_publicar", "O nome fantasia é obrigatório para publicar o perfil.");
    public static Error HorarioNaoEncontrado => Error.NotFound("perfil_publico.horario_nao_encontrado", "Horário de funcionamento não encontrado.");
    public static Error HorariosExcedemLimite => Error.Validation("perfil_publico.horarios_excedem_limite", $"A lista de horários de funcionamento não pode ter mais de {ParametrosDerivacao.MaxHorariosFuncionamento} itens.");
}
