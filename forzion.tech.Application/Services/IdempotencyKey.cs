using System.Globalization;

namespace forzion.tech.Application.Services;

public static class IdempotencyKey
{
    public static string Cobranca(string tipo, Guid assinaturaId, DateTime instante, Guid? discriminador = null)
    {
        var bucket = instante.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture);
        return discriminador is null
            ? $"cobr:{tipo}:{assinaturaId}:{bucket}"
            : $"cobr:{tipo}:{assinaturaId}:{discriminador}:{bucket}";
    }
}
