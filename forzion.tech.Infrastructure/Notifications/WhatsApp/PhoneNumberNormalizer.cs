using forzion.tech.Domain.Shared;

namespace forzion.tech.Infrastructure.Notifications.WhatsApp;

/// <summary>
/// Normaliza telefone para o formato esperado pela Meta Cloud API: dígitos E.164 SEM "+".
/// Default de país: Brasil (DDI 55). Retorna null quando não há como normalizar com segurança.
/// </summary>
public static class PhoneNumberNormalizer
{
    public static string? Normalizar(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;

        var digitos = Digitos.Apenas(phone);
        if (digitos.Length == 0)
            return null;

        // Já tem DDI BR (55 + 10/11 dígitos locais).
        if (digitos.StartsWith("55", StringComparison.Ordinal) && digitos.Length is 12 or 13)
            return digitos;

        // Número local BR: 10 (fixo) ou 11 (celular com 9) → prefixa DDI 55.
        if (digitos.Length is 10 or 11)
            return "55" + digitos;

        // Assume já internacional (qualquer DDI) quando comprimento plausível E.164.
        if (digitos.Length is >= 11 and <= 15)
            return digitos;

        return null;
    }
}
