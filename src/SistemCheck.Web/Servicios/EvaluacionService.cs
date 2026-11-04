using SistemCheck.Web.Datos;
using SistemCheck.Web.Dominio;

namespace SistemCheck.Web.Servicios;

public record Evaluacion(
    InformeBcra Informe,
    Dictamen Dictamen,
    DateTime ConsultadoEnUtc,
    bool DesdeCache);

/// <summary>Caso de uso compartido por las páginas Razor y la API REST.</summary>
public class EvaluacionService(IFuenteInformes fuente, AppDbContext db, TimeProvider tiempo)
{
    public async Task<Evaluacion> EvaluarAsync(string cuit, int? usuarioId, CancellationToken ct = default)
    {
        var obtenido = await fuente.ObtenerAsync(cuit, ct);
        var dictamen = EvaluadorRiesgo.Evaluar(obtenido.Informe);

        db.Consultas.Add(new Consulta
        {
            UsuarioId = usuarioId,
            Cuit = cuit,
            Denominacion = obtenido.Informe.Denominacion,
            Semaforo = dictamen.Semaforo,
            PeorSituacion = obtenido.Informe.PeorSituacion,
            DeudaTotal = obtenido.Informe.DeudaTotal,
            FechaUtc = tiempo.GetUtcNow().UtcDateTime
        });
        await db.SaveChangesAsync(ct);

        return new Evaluacion(obtenido.Informe, dictamen, obtenido.ConsultadoEnUtc, obtenido.DesdeCache);
    }
}
