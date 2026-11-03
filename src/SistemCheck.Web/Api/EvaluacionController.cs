using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using SistemCheck.Web.Dominio;
using SistemCheck.Web.Servicios;

namespace SistemCheck.Web.Api;

[ApiController]
[Route("api/evaluacion")]
public class EvaluacionController(EvaluacionService evaluaciones) : ControllerBase
{
    /// <summary>Devuelve el informe consolidado y el semáforo de riesgo de un CUIT/CUIL.</summary>
    [HttpGet("{cuit}")]
    [ProducesResponseType<EvaluacionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<EvaluacionResponse>> Get(
        string cuit,
        [FromQuery, Range(0.01, 1e12)] decimal? monto,
        CancellationToken ct)
    {
        if (!Cuit.TryNormalizar(cuit, out var cuitNormalizado))
            return Problem("El CUIT/CUIL no es válido.", statusCode: StatusCodes.Status400BadRequest);

        try
        {
            var evaluacion = await evaluaciones.EvaluarAsync(cuitNormalizado, monto, usuarioId: null, ct);
            return EvaluacionResponse.Desde(evaluacion);
        }
        catch (BcraNoDisponibleException)
        {
            return Problem("El servicio del BCRA no está disponible. Intente nuevamente más tarde.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}

public record EvaluacionResponse(
    string Cuit,
    string? Denominacion,
    string Semaforo,
    string Resultado,
    string Resumen,
    int? PeorSituacion,
    decimal DeudaTotal,
    decimal? MontoSolicitado,
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
        e.MontoSolicitado,
        e.Informe.ChequesRechazados,
        e.Informe.ChequesImpagos,
        e.Informe.MontoChequesImpagos,
        e.Informe.Periodo,
        e.Informe.Deudas,
        e.ConsultadoEnUtc,
        e.DesdeCache);
}
