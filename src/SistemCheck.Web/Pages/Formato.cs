using System.Globalization;
using SistemCheck.Web.Dominio;

namespace SistemCheck.Web.Pages;

public static class Formato
{
    private static readonly CultureInfo Argentina = CultureInfo.GetCultureInfo("es-AR");
    private static readonly TimeZoneInfo HoraArgentina = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");

    public static string Pesos(decimal monto) => monto.ToString("C2", Argentina);

    public static string Fecha(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), HoraArgentina).ToString("dd/MM/yyyy HH:mm", Argentina);

    /// <summary>"202609" pasa a "09/2026".</summary>
    public static string Periodo(string? periodo) =>
        periodo is { Length: 6 } ? $"{periodo[4..]}/{periodo[..4]}" : "-";

    public static string ClaseSemaforo(Semaforo semaforo) => semaforo switch
    {
        Semaforo.Verde => "text-bg-success",
        Semaforo.Amarillo => "text-bg-warning",
        _ => "text-bg-danger"
    };

    public static string TextoSemaforo(Semaforo semaforo) => semaforo switch
    {
        Semaforo.Verde => "APROBADO",
        Semaforo.Amarillo => "PRECAUCIÓN",
        _ => "RECHAZADO"
    };
}
