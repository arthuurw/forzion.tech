namespace forzion.tech.Api.Extensions;

public static class ProblemDetailsTitulos
{
    public static readonly IReadOnlyDictionary<int, string> PtBr = new Dictionary<int, string>
    {
        [StatusCodes.Status400BadRequest] = "Requisição inválida.",
        [StatusCodes.Status401Unauthorized] = "Não autorizado.",
        [StatusCodes.Status403Forbidden] = "Acesso negado.",
        [StatusCodes.Status404NotFound] = "Não encontrado.",
        [StatusCodes.Status405MethodNotAllowed] = "Método não permitido.",
        [StatusCodes.Status409Conflict] = "Conflito.",
        [StatusCodes.Status415UnsupportedMediaType] = "Formato de mídia não suportado.",
        [StatusCodes.Status422UnprocessableEntity] = "Não foi possível processar.",
        [StatusCodes.Status429TooManyRequests] = "Muitas requisições. Tente novamente em instantes.",
        [StatusCodes.Status500InternalServerError] = "Erro interno.",
        [StatusCodes.Status502BadGateway] = "Serviço externo indisponível.",
    };
}
