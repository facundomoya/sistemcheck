using System.Net;
using System.Text.Json;
using SistemCheck.Web.Dominio;

namespace SistemCheck.Web.Servicios;

/// <summary>Patrón Decorator - Componente concreto: consulta la API pública del BCRA.</summary>
public class BcraApiClient(HttpClient http, TimeProvider tiempo) : IFuenteInformes
{
    // La Central de Deudores informa las deudas en miles de pesos; los cheques vienen en pesos.
    private const decimal MilesDePesos = 1000m;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<InformeObtenido> ObtenerAsync(string cuit, CancellationToken ct = default)
    {
        var deudasTask = GetResultsAsync<DeudasDto>($"Deudas/{cuit}", ct);
        var chequesTask = GetResultsAsync<ChequesDto>($"Deudas/ChequesRechazados/{cuit}", ct);
        await Task.WhenAll(deudasTask, chequesTask);

        var deudas = deudasTask.Result;
        var cheques = chequesTask.Result;

        var periodo = deudas?.Periodos?.MaxBy(p => p.Periodo, StringComparer.Ordinal);
        var detalleCheques = cheques?.Causales?
            .SelectMany(c => c.Entidades ?? [])
            .SelectMany(e => e.Detalle ?? [])
            .ToList() ?? [];
        var impagos = detalleCheques.Where(c => c.FechaPago is null).ToList();

        var informe = new InformeBcra(
            cuit,
            deudas?.Denominacion ?? cheques?.Denominacion,
            periodo?.Periodo,
            periodo?.Entidades?
                .Select(e => new DeudaEntidad(e.Entidad ?? "Sin informar", e.Situacion, e.Monto * MilesDePesos, e.DiasAtrasoPago))
                .ToList() ?? [],
            detalleCheques.Count,
            impagos.Count,
            impagos.Sum(c => c.Monto));

        return new InformeObtenido(informe, tiempo.GetUtcNow().UtcDateTime, DesdeCache: false);
    }

    /// <summary>Devuelve null si el BCRA responde 404, que significa "sin datos para ese CUIT".</summary>
    private async Task<T?> GetResultsAsync<T>(string ruta, CancellationToken ct) where T : class
    {
        try
        {
            using var respuesta = await http.GetAsync(ruta, ct);

            if (respuesta.StatusCode == HttpStatusCode.NotFound)
                return null;

            if (!respuesta.IsSuccessStatusCode)
                throw new BcraNoDisponibleException($"El BCRA respondió {(int)respuesta.StatusCode}.");

            var cuerpo = await respuesta.Content.ReadFromJsonAsync<Respuesta<T>>(Json, ct);
            return cuerpo?.Results;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
                                   || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            throw new BcraNoDisponibleException("No se pudo consultar el BCRA.", ex);
        }
    }

    private record Respuesta<T>(T? Results);

    private record DeudasDto(string? Denominacion, List<PeriodoDto>? Periodos);
    private record PeriodoDto(string Periodo, List<EntidadDeudaDto>? Entidades);
    private record EntidadDeudaDto(string? Entidad, int Situacion, decimal Monto, int? DiasAtrasoPago);

    private record ChequesDto(string? Denominacion, List<CausalDto>? Causales);
    private record CausalDto(List<EntidadChequesDto>? Entidades);
    private record EntidadChequesDto(List<ChequeDto>? Detalle);
    private record ChequeDto(decimal Monto, string? FechaPago);
}
