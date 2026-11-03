namespace SistemCheck.Web.Dominio;

public static class Cuit
{
    private static readonly int[] Pesos = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];

    /// <summary>Quita guiones y espacios, y valida longitud y dígito verificador.</summary>
    public static bool TryNormalizar(string? entrada, out string cuit)
    {
        var digitos = (entrada ?? "").Replace("-", "").Replace(" ", "");
        cuit = digitos;

        if (digitos.Length != 11 || !digitos.All(char.IsAsciiDigit))
            return false;

        var suma = Pesos.Select((peso, i) => peso * (digitos[i] - '0')).Sum();
        var verificador = 11 - suma % 11;
        if (verificador == 11) verificador = 0;

        return verificador == digitos[10] - '0';
    }
}
