namespace forzion.tech.Domain.Shared.Errors;

public static class NotaFiscalErrors
{
    public static Error TreinadorIdInvalido => new("nota_fiscal.treinador_id_invalido", "O identificador do treinador é inválido.");
    public static Error PagamentoIdInvalido => new("nota_fiscal.pagamento_id_invalido", "O identificador do pagamento é inválido.");
    public static Error ValorInvalido => new("nota_fiscal.valor_invalido", "O valor da nota fiscal deve ser maior que zero.");
    public static Error CompetenciaInvalida => new("nota_fiscal.competencia_invalida", "A competência da nota fiscal é inválida.");
    public static Error ChaveAcessoObrigatoria => new("nota_fiscal.chave_acesso_obrigatoria", "A chave de acesso da NFS-e é obrigatória.");
    public static Error TransicaoEmissaoInvalida => new("nota_fiscal.transicao_emissao_invalida", "A nota fiscal não pode ser emitida no status atual.");
    public static Error TransicaoErroInvalida => new("nota_fiscal.transicao_erro_invalida", "A nota fiscal não pode ser marcada com erro no status atual.");
    public static Error TransicaoBloqueioInvalida => new("nota_fiscal.transicao_bloqueio_invalida", "A nota fiscal não pode ser bloqueada no status atual.");
    public static Error TransicaoCancelamentoInvalida => new("nota_fiscal.transicao_cancelamento_invalida", "O cancelamento só pode ser solicitado para uma nota emitida.");
    public static Error TransicaoCanceladaInvalida => new("nota_fiscal.transicao_cancelada_invalida", "A nota só pode ser marcada como cancelada após solicitação de cancelamento.");
    public static Error TransicaoExpiradoInvalida => new("nota_fiscal.transicao_expirado_invalida", "O cancelamento só pode expirar após ter sido solicitado.");
}
