using forzion.tech.Domain.Services;

namespace forzion.tech.Domain.Shared.Errors;

public static class PerfilPublicoErrors
{
    public static Error NomeFantasiaObrigatorioParaPublicar => Error.Validation("perfil_publico.nome_fantasia_obrigatorio_para_publicar", "O nome fantasia é obrigatório para publicar o perfil.");
    public static Error HorarioNaoEncontrado => Error.NotFound("perfil_publico.horario_nao_encontrado", "Horário de funcionamento não encontrado.");
    public static Error HorariosExcedemLimite => Error.Validation("perfil_publico.horarios_excedem_limite", $"A lista de horários de funcionamento não pode ter mais de {ParametrosDerivacao.MaxHorariosFuncionamento} itens.");
    public static Error NomeFantasiaMuitoLongo => Error.Validation("perfil_publico.nome_fantasia_muito_longo", $"O nome fantasia deve ter no máximo {ParametrosDerivacao.MaxNomeFantasiaLength} caracteres.");
    public static Error PoliticasExcedemLimite => Error.Validation("perfil_publico.politicas_excedem_limite", $"A lista de políticas não pode ter mais de {ParametrosDerivacao.MaxPoliticas} itens.");
    public static Error PoliticaChaveMuitoLonga => Error.Validation("perfil_publico.politica_chave_muito_longa", $"A chave de uma política deve ter no máximo {ParametrosDerivacao.MaxPoliticaChaveLength} caracteres.");
    public static Error PoliticaValorMuitoLongo => Error.Validation("perfil_publico.politica_valor_muito_longo", $"O valor de uma política deve ter no máximo {ParametrosDerivacao.MaxPoliticaValorLength} caracteres.");
}
