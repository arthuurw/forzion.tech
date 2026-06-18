namespace forzion.tech.Domain.Shared;

public static class Digitos
{
    public static string Apenas(string? valor) =>
        string.IsNullOrEmpty(valor) ? string.Empty : new string(valor.Where(char.IsDigit).ToArray());
}
