using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SistemCheck.Web.Datos;
using SistemCheck.Web.Dominio;
using SistemCheck.Web.Servicios;

namespace SistemCheck.Tests;

/// <summary>
/// Prueba la API por HTTP, con todo el pipeline de ASP.NET Core: SQLite en memoria en lugar de MySQL
/// y una fuente de informes falsa en lugar del BCRA.
/// </summary>
public sealed class ApiTests : IDisposable
{
    private const string CuitValido = "30500010912";
    private const string OrigenPermitido = "http://grupo-permitido.test";

    private readonly SqliteConnection _conexion = new("Data Source=:memory:");
    private readonly FuenteFalsa _fuente = new();
    private readonly WebApplicationFactory<Program> _app;
    private readonly HttpClient _http;

    public ApiTests()
    {
        _conexion.Open();

        _app = new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
        {
            web.UseSetting("BaseDeDatos:MigrarAlIniciar", "false");
            web.UseSetting("Cors:OrigenesPermitidos:0", OrigenPermitido);
            web.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options => options.UseSqlite(_conexion));

                services.RemoveAll<IFuenteInformes>();
                services.AddSingleton<IFuenteInformes>(_fuente);
            });
        });

        using (var scope = _app.Services.CreateScope())
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();

        _http = _app.CreateClient();
    }

    [Fact]
    public async Task Cuit_valido_devuelve_200_con_el_contrato_json()
    {
        var respuesta = await _http.GetAsync("/api/evaluacion/30-50001091-2?monto=1500.50");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("application/json", respuesta.Content.Headers.ContentType?.MediaType);

        var json = await LeerJson(respuesta);
        Assert.Equal(CuitValido, json.GetProperty("cuit").GetString());
        Assert.Equal("Verde", json.GetProperty("semaforo").GetString());
        Assert.Equal("APROBADO", json.GetProperty("resultado").GetString());
        Assert.Equal(1500.50m, json.GetProperty("montoSolicitado").GetDecimal());
        Assert.Equal(1, json.GetProperty("deudas").GetArrayLength());
        Assert.False(json.GetProperty("desdeCache").GetBoolean());
    }

    [Fact]
    public async Task Cuit_invalido_devuelve_problem_details_400()
    {
        var respuesta = await _http.GetAsync("/api/evaluacion/30500010913");

        var problema = await AssertProblemDetails(respuesta, HttpStatusCode.BadRequest);
        Assert.Equal("El CUIT/CUIL no es válido.", problema.GetProperty("detail").GetString());
        Assert.Equal(0, _fuente.Llamadas);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    public async Task Monto_incorrecto_devuelve_problem_details_400_con_errores(string monto)
    {
        var respuesta = await _http.GetAsync($"/api/evaluacion/{CuitValido}?monto={monto}");

        var problema = await AssertProblemDetails(respuesta, HttpStatusCode.BadRequest);
        Assert.True(problema.GetProperty("errors").TryGetProperty("monto", out _));
        Assert.Equal(0, _fuente.Llamadas);
    }

    [Fact]
    public async Task Bcra_no_disponible_devuelve_problem_details_503()
    {
        _fuente.Error = new BcraNoDisponibleException("El BCRA respondió 500.");

        var respuesta = await _http.GetAsync($"/api/evaluacion/{CuitValido}");

        var problema = await AssertProblemDetails(respuesta, HttpStatusCode.ServiceUnavailable);
        Assert.Contains("BCRA no está disponible", problema.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Error_inesperado_devuelve_problem_details_500_sin_detalles_internos()
    {
        _fuente.Error = new InvalidOperationException("Detalle interno secreto");

        var respuesta = await _http.GetAsync($"/api/evaluacion/{CuitValido}");

        var problema = await AssertProblemDetails(respuesta, HttpStatusCode.InternalServerError);
        Assert.Equal("Ocurrió un error inesperado. Intente nuevamente más tarde.", problema.GetProperty("detail").GetString());

        var cuerpo = problema.GetRawText();
        Assert.DoesNotContain("secreto", cuerpo);
        Assert.DoesNotContain("InvalidOperationException", cuerpo);
        Assert.DoesNotContain("stack", cuerpo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ruta_inexistente_de_la_api_devuelve_problem_details_404()
    {
        var respuesta = await _http.GetAsync("/api/no-existe");

        await AssertProblemDetails(respuesta, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Cors_permite_solo_los_origenes_configurados()
    {
        var permitida = await GetConOrigen(OrigenPermitido);
        var rechazada = await GetConOrigen("http://otro-sitio.test");

        Assert.Equal(OrigenPermitido, permitida.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.False(rechazada.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Cors_responde_la_solicitud_previa_del_navegador()
    {
        using var preflight = new HttpRequestMessage(HttpMethod.Options, $"/api/evaluacion/{CuitValido}");
        preflight.Headers.Add("Origin", OrigenPermitido);
        preflight.Headers.Add("Access-Control-Request-Method", "GET");

        var respuesta = await _http.SendAsync(preflight);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        Assert.Equal(OrigenPermitido, respuesta.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task La_documentacion_openapi_esta_publicada()
    {
        var respuesta = await _http.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var documento = await LeerJson(respuesta);
        Assert.True(documento.GetProperty("paths").TryGetProperty("/api/evaluacion/{cuit}", out var ruta));

        var respuestas = ruta.GetProperty("get").GetProperty("responses");
        foreach (var codigo in new[] { "200", "400", "500", "503" })
            Assert.True(respuestas.TryGetProperty(codigo, out _), $"Falta documentar el código {codigo}.");
    }

    public void Dispose()
    {
        _http.Dispose();
        _app.Dispose();
        _conexion.Dispose();
    }

    private async Task<HttpResponseMessage> GetConOrigen(string origen)
    {
        using var solicitud = new HttpRequestMessage(HttpMethod.Get, $"/api/evaluacion/{CuitValido}");
        solicitud.Headers.Add("Origin", origen);
        return await _http.SendAsync(solicitud);
    }

    private static async Task<JsonElement> AssertProblemDetails(HttpResponseMessage respuesta, HttpStatusCode esperado)
    {
        Assert.Equal(esperado, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);

        var problema = await LeerJson(respuesta);
        Assert.Equal((int)esperado, problema.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(problema.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrEmpty(problema.GetProperty("type").GetString()));
        Assert.True(problema.TryGetProperty("traceId", out _));
        return problema;
    }

    private static async Task<JsonElement> LeerJson(HttpResponseMessage respuesta) =>
        JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement;

    private sealed class FuenteFalsa : IFuenteInformes
    {
        public int Llamadas { get; private set; }
        public Exception? Error { get; set; }

        public Task<InformeObtenido> ObtenerAsync(string cuit, CancellationToken ct = default)
        {
            Llamadas++;
            if (Error is not null) throw Error;

            var informe = new InformeBcra(cuit, "EMPRESA SA", "202609", [new DeudaEntidad("BANCO A", 1, 100000m, 0)], 0, 0, 0m);
            return Task.FromResult(new InformeObtenido(informe, DateTime.UtcNow, DesdeCache: false));
        }
    }
}
