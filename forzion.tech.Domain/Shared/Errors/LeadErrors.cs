namespace forzion.tech.Domain.Shared.Errors;

public static class LeadErrors
{
    public static Error TreinadorIdInvalido => Error.Validation("lead.treinador_id_invalido", "O treinador do lead é obrigatório.");
    public static Error NomeObrigatorio => Error.Validation("lead.nome_obrigatorio", "O nome do lead é obrigatório.");
    public static Error NomeMuitoLongo => Error.Validation("lead.nome_muito_longo", "O nome do lead deve ter no máximo 200 caracteres.");
    public static Error InteresseMuitoLongo => Error.Validation("lead.interesse_muito_longo", "O interesse do lead deve ter no máximo 1000 caracteres.");
    public static Error ObservacaoMuitoLonga => Error.Validation("lead.observacao_muito_longa", "A observação deve ter no máximo 1000 caracteres.");
    public static Error ObservacaoObrigatoria => Error.Validation("lead.observacao_obrigatoria", "A observação é obrigatória.");
    public static Error AlunoIdInvalido => Error.Business("lead.aluno_id_invalido", "O aluno resultante da conversão é obrigatório.");
    public static Error JaEmContato => Error.Business("lead.ja_em_contato", "Este lead já está em contato.");
    public static Error EstadoTerminal => Error.Business("lead.estado_terminal", "Este lead já foi finalizado.");
    public static Error Anonimizado => Error.Business("lead.anonimizado", "Este lead foi anonimizado por política de retenção.");
    public static Error NaoEncontrado => Error.NotFound("lead.nao_encontrado", "Lead não encontrado.");
    public static Error TransicaoNaoSuportada => Error.Validation("lead.transicao_nao_suportada", "Esta transição de status não é suportada por esta operação.");
    public static Error MotivoDescarteObrigatorio => Error.Validation("lead.motivo_descarte_obrigatorio", "O motivo do descarte é obrigatório.");
}
