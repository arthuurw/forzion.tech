namespace forzion.tech.Domain.Shared.Errors;

public static class NotaFiscalErrors
{
    public static Error TreinadorIdInvalido => Error.Validation("nota_fiscal.treinador_id_invalido", "O identificador do treinador é inválido.");
    public static Error PagamentoIdInvalido => Error.Validation("nota_fiscal.pagamento_id_invalido", "O identificador do pagamento é inválido.");
    public static Error ValorInvalido => Error.Validation("nota_fiscal.valor_invalido", "O valor da nota fiscal deve ser maior que zero.");
    public static Error CompetenciaInvalida => Error.Validation("nota_fiscal.competencia_invalida", "A competência da nota fiscal é inválida.");
    public static Error ChaveAcessoObrigatoria => Error.Validation("nota_fiscal.chave_acesso_obrigatoria", "A chave de acesso da NFS-e é obrigatória.");
    public static Error TransicaoEmissaoInvalida => Error.Conflict("nota_fiscal.transicao_emissao_invalida", "A nota fiscal não pode ser emitida no status atual.");
    public static Error TransicaoErroInvalida => Error.Conflict("nota_fiscal.transicao_erro_invalida", "A nota fiscal não pode ser marcada com erro no status atual.");
    public static Error TransicaoBloqueioInvalida => Error.Conflict("nota_fiscal.transicao_bloqueio_invalida", "A nota fiscal não pode ser bloqueada no status atual.");
    public static Error TransicaoCancelamentoInvalida => Error.Conflict("nota_fiscal.transicao_cancelamento_invalida", "O cancelamento só pode ser solicitado para uma nota emitida.");
    public static Error TransicaoCanceladaInvalida => Error.Conflict("nota_fiscal.transicao_cancelada_invalida", "A nota só pode ser marcada como cancelada após solicitação de cancelamento.");
    public static Error TransicaoExpiradoInvalida => Error.Conflict("nota_fiscal.transicao_expirado_invalida", "O cancelamento só pode expirar após ter sido solicitado.");
    public static Error TransicaoReaberturaInvalida => Error.Conflict("nota_fiscal.transicao_reabertura_invalida", "A nota fiscal só pode ser reaberta para emissão quando bloqueada por dados fiscais.");
    public static Error TransicaoCancelamentoPendenteInvalida => Error.Conflict("nota_fiscal.transicao_cancelamento_pendente_invalida", "O cancelamento pré-emissão só pode ser registrado antes de a nota ser emitida.");
    public static Error NaoEncontrada => Error.NotFound("nota_fiscal.nao_encontrada", "Nota fiscal não encontrada.");
    public static Error DanfseIndisponivel => Error.NotFound("nota_fiscal.danfse_indisponivel", "DANFSe não disponível para esta nota fiscal.");
    public static Error ReprocessamentoInvalido => Error.Conflict("nota_fiscal.reprocessamento_invalido", "Apenas notas fiscais em erro podem ser reprocessadas.");
}
