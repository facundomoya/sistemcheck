using SistemCheck.Web.Dominio;

namespace SistemCheck.Web.Servicios;

/// <summary>
/// Patrón Decorator - Componente: lo que el resto del sistema sabe sobre cómo conseguir un informe.
/// Lo implementan <see cref="BcraApiClient"/> (la API real) y <see cref="FuenteInformesConCache"/> (la envuelve).
/// </summary>
public interface IFuenteInformes
{
    Task<InformeObtenido> ObtenerAsync(string cuit, CancellationToken ct = default);
}

public record InformeObtenido(InformeBcra Informe, DateTime ConsultadoEnUtc, bool DesdeCache);

public class BcraNoDisponibleException(string message, Exception? inner = null) : Exception(message, inner);
