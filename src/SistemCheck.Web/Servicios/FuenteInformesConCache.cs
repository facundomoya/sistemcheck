using System.Text.Json;
using SistemCheck.Web.Datos;
using SistemCheck.Web.Dominio;

namespace SistemCheck.Web.Servicios;

/// <summary>
/// Patrón Decorator - Decorador: agrega el caché de 24 horas en MySQL a cualquier
/// <see cref="IFuenteInformes"/>, sin que quien la envuelve ni quien la usa se enteren.
/// </summary>
public class FuenteInformesConCache(IFuenteInformes interna, AppDbContext db, TimeProvider tiempo) : IFuenteInformes
{
    public static readonly TimeSpan Vigencia = TimeSpan.FromHours(24);

    public async Task<InformeObtenido> ObtenerAsync(string cuit, CancellationToken ct = default)
    {
        var cacheado = await db.InformesCacheados.FindAsync([cuit], ct);
        var ahora = tiempo.GetUtcNow().UtcDateTime;

        if (cacheado is not null && ahora - cacheado.ConsultadoEnUtc < Vigencia)
        {
            var informe = JsonSerializer.Deserialize<InformeBcra>(cacheado.Json)!;
            // La base no guarda el Kind: sin esto la API devolvería la fecha sin la "Z" de UTC.
            var consultadoEnUtc = DateTime.SpecifyKind(cacheado.ConsultadoEnUtc, DateTimeKind.Utc);
            return new InformeObtenido(informe, consultadoEnUtc, DesdeCache: true);
        }

        var obtenido = await interna.ObtenerAsync(cuit, ct);
        var json = JsonSerializer.Serialize(obtenido.Informe);

        if (cacheado is null)
        {
            db.InformesCacheados.Add(new InformeCacheado { Cuit = cuit, Json = json, ConsultadoEnUtc = obtenido.ConsultadoEnUtc });
        }
        else
        {
            cacheado.Json = json;
            cacheado.ConsultadoEnUtc = obtenido.ConsultadoEnUtc;
        }

        await db.SaveChangesAsync(ct);
        return obtenido;
    }
}
