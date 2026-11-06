using Microsoft.AspNetCore.Mvc;
using SistemCheck.Web.Dominio;
using SistemCheck.Web.Servicios;

namespace SistemCheck.Web.Api;

[ApiController]
[Route("api/evaluacion")]
public class EvaluacionController(EvaluacionService evaluaciones, ILogger<EvaluacionController> logger) : ControllerBase
{
    private const string Json = "application/json";
    private const string ProblemJson = "application/problem+json";

    /// <summary>Devuelve el informe consolidado y el semáforo de riesgo de un CUIT/CUIL.</summary>
    /// <remarks>
    /// Consulta la Central de Deudores del BCRA (o el caché local de 24 horas) y aplica las reglas del semáforo:
    /// situación 1 es Verde (APROBADO), situación 2 o sin deudas informadas es Amarillo (PRECAUCIÓN), y
    /// situación 3 o más es Rojo (RECHAZADO). Los montos están en pesos.
    ///
    /// Ejemplo de solicitud:
    ///
    ///     GET /api/evaluacion/30-50001091-2
    ///
    /// Ejemplo de respuesta 200 (resumida):
    ///
    ///     {
    ///       "cuit": "30500010912",
    ///       "denominacion": "BANCO DE LA NACION ARGENTINA",
    ///       "semaforo": "Verde",
    ///       "resultado": "APROBADO",
    ///       "resumen": "Situación 1: cliente sin mora o con atrasos menores a 31 días.",
    ///       "peorSituacion": 1,
    ///       "deudaTotal": 329154542000.0,
    ///       "chequesRechazados": 0,
    ///       "chequesImpagos": 0,
    ///       "montoChequesImpagos": 0,
    ///       "periodo": "202609",
    ///       "deudas": [ { "entidad": "BANCO EJEMPLO S.A.", "situacion": 1, "monto": 1500000.0, "diasAtraso": 0 } ],
    ///       "consultadoEnUtc": "2026-11-03T18:00:00Z",
    ///       "desdeCache": false
    ///     }
    ///
    /// Los errores usan ProblemDetails (RFC 7807), con Content-Type `application/problem+json`:
    ///
    ///     {
    ///       "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
    ///       "title": "Bad Request",
    ///       "status": 400,
    ///       "detail": "El CUIT/CUIL no es válido.",
    ///       "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00"
    ///     }
    /// </remarks>
    /// <param name="cuit" example="30-50001091-2">CUIT/CUIL de 11 dígitos, con o sin guiones. Obligatorio.</param>
    /// <param name="ct">Cancelación de la solicitud.</param>
    /// <response code="200">Informe consolidado y semáforo.</response>
    /// <response code="400">CUIT/CUIL inválido.</response>
    /// <response code="429">Se superó el límite de consultas por minuto de esta IP.</response>
    /// <response code="503">El servicio del BCRA no está disponible y no hay un informe vigente en caché.</response>
    /// <response code="500">Error inesperado del servidor. No incluye detalles internos.</response>
    [HttpGet("{cuit}")]
    [ProducesResponseType<EvaluacionResponse>(StatusCodes.Status200OK, Json)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, ProblemJson)]
    public async Task<ActionResult<EvaluacionResponse>> Get(
        string cuit,
        CancellationToken ct)
    {
        if (!Cuit.TryNormalizar(cuit, out var cuitNormalizado))
            return Problem("El CUIT/CUIL no es válido.", statusCode: StatusCodes.Status400BadRequest);

        try
        {
            var evaluacion = await evaluaciones.EvaluarAsync(cuitNormalizado, usuarioId: null, ct);
            return EvaluacionResponse.Desde(evaluacion);
        }
        catch (BcraNoDisponibleException ex)
        {
            logger.LogWarning(ex, "No se pudo obtener el informe del BCRA.");
            return Problem("El servicio del BCRA no está disponible. Intente nuevamente más tarde.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}

/// <summary>Resultado de la evaluación de riesgo de un CUIT/CUIL.</summary>
/// <param name="Cuit">CUIT/CUIL normalizado: 11 dígitos, sin guiones.</param>
/// <param name="Denominacion">Razón social o nombre informado por el BCRA. Null si no hay datos.</param>
/// <param name="Semaforo">Verde, Amarillo o Rojo.</param>
/// <param name="Resultado">APROBADO, PRECAUCIÓN o RECHAZADO.</param>
/// <param name="Resumen">Explicación del resultado, en texto.</param>
/// <param name="PeorSituacion">Peor situación (1 a 5) entre todas las entidades en el último período. Null si no hay deudas informadas.</param>
/// <param name="DeudaTotal">Deuda total en el sistema financiero, en pesos.</param>
/// <param name="ChequesRechazados">Cantidad de cheques rechazados informados.</param>
/// <param name="ChequesImpagos">Cantidad de cheques rechazados que siguen sin pagar.</param>
/// <param name="MontoChequesImpagos">Suma de los cheques impagos, en pesos.</param>
/// <param name="Periodo">Último período informado, con formato AAAAMM. Null si no hay deudas.</param>
/// <param name="Deudas">Deudas por entidad en el último período.</param>
/// <param name="ConsultadoEnUtc">Fecha y hora (UTC) en que se obtuvo el informe del BCRA.</param>
/// <param name="DesdeCache">True si el informe salió del caché local (vigencia de 24 horas).</param>
public record EvaluacionResponse(
    string Cuit,
    string? Denominacion,
    string Semaforo,
    string Resultado,
    string Resumen,
    int? PeorSituacion,
    decimal DeudaTotal,
    int ChequesRechazados,
    int ChequesImpagos,
    decimal MontoChequesImpagos,
    string? Periodo,
    List<DeudaEntidad> Deudas,
    DateTime ConsultadoEnUtc,
    bool DesdeCache)
{
    public static EvaluacionResponse Desde(Evaluacion e) => new(
        e.Informe.Cuit,
        e.Informe.Denominacion,
        e.Dictamen.Semaforo.ToString(),
        e.Dictamen.Resultado,
        e.Dictamen.Resumen,
        e.Informe.PeorSituacion,
        e.Informe.DeudaTotal,
        e.Informe.ChequesRechazados,
        e.Informe.ChequesImpagos,
        e.Informe.MontoChequesImpagos,
        e.Informe.Periodo,
        e.Informe.Deudas,
        e.ConsultadoEnUtc,
        e.DesdeCache);
}
