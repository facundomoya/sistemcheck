using System.Net;
using System.Text;
using SistemCheck.Web.Servicios;

namespace SistemCheck.Tests;

public class BcraApiClientTests
{
    private const string Cuit = "30500010912";

    private const string SinDatos = """{"status":404,"errorMessages":["No se encontró datos para la identificación ingresada."]}""";

    private const string Deudas = """
        {"status":200,"results":{"identificacion":30500010912,"denominacion":"EMPRESA SA","periodos":[
          {"periodo":"202608","entidades":[{"entidad":"BANCO VIEJO","situacion":5,"monto":999.0,"diasAtrasoPago":400}]},
          {"periodo":"202609","entidades":[
            {"entidad":"BANCO A","situacion":1,"monto":100.5,"diasAtrasoPago":0},
            {"entidad":"BANCO B","situacion":2,"monto":200.0,"diasAtrasoPago":45}]}
        ]}}
        """;

    private const string Cheques = """
        {"status":200,"results":{"identificacion":30500010912,"denominacion":"EMPRESA SA","causales":[
          {"causal":"SIN FONDOS","entidades":[{"entidad":1,"detalle":[
            {"nroCheque":1,"fechaRechazo":"2024-04-08","monto":115000.00,"fechaPago":null,"estadoMulta":"IMPAGA"},
            {"nroCheque":2,"fechaRechazo":"2024-04-09","monto":50000.00,"fechaPago":"2024-05-01"}]}]},
          {"causal":"DEFECTOS FORMALES","entidades":[{"entidad":7,"detalle":[
            {"nroCheque":3,"fechaRechazo":"2024-06-01","monto":1000.00,"fechaPago":null}]}]}
        ]}}
        """;

    [Fact]
    public async Task Convierte_miles_a_pesos_y_usa_solo_el_ultimo_periodo()
    {
        var resultado = await Cliente(Deudas, SinDatos).ObtenerAsync(Cuit);

        Assert.Equal("EMPRESA SA", resultado.Informe.Denominacion);
        Assert.Equal("202609", resultado.Informe.Periodo);
        Assert.Equal(2, resultado.Informe.PeorSituacion);
        Assert.Equal(300500m, resultado.Informe.DeudaTotal);
        Assert.Equal(0, resultado.Informe.ChequesRechazados);
    }

    [Fact]
    public async Task Cuenta_cheques_en_pesos_de_todas_las_causales()
    {
        var resultado = await Cliente(SinDatos, Cheques).ObtenerAsync(Cuit);

        Assert.Equal(3, resultado.Informe.ChequesRechazados);
        Assert.Equal(2, resultado.Informe.ChequesImpagos);
        Assert.Equal(116000m, resultado.Informe.MontoChequesImpagos);
        Assert.Equal("EMPRESA SA", resultado.Informe.Denominacion);
    }

    [Fact]
    public async Task Un_404_significa_sin_datos_y_no_es_un_error()
    {
        var resultado = await Cliente(SinDatos, SinDatos).ObtenerAsync(Cuit);

        Assert.Null(resultado.Informe.PeorSituacion);
        Assert.Equal(0m, resultado.Informe.DeudaTotal);
    }

    [Fact]
    public async Task Un_500_del_bcra_es_servicio_no_disponible()
    {
        var cliente = Cliente(Deudas, Cheques, HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<BcraNoDisponibleException>(() => cliente.ObtenerAsync(Cuit));
    }

    [Fact]
    public async Task Una_respuesta_malformada_es_servicio_no_disponible()
    {
        var cliente = Cliente("<html>mantenimiento</html>", SinDatos);

        await Assert.ThrowsAsync<BcraNoDisponibleException>(() => cliente.ObtenerAsync(Cuit));
    }

    private static BcraApiClient Cliente(string deudas, string cheques, HttpStatusCode? forzar = null)
    {
        var http = new HttpClient(new HandlerFalso(deudas, cheques, forzar))
        {
            BaseAddress = new Uri("https://bcra.test/v1.0/")
        };
        return new BcraApiClient(http, TimeProvider.System);
    }

    private sealed class HandlerFalso(string deudas, string cheques, HttpStatusCode? forzar) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var cuerpo = request.RequestUri!.AbsolutePath.Contains("ChequesRechazados") ? cheques : deudas;
            var estado = forzar ?? (cuerpo == SinDatos ? HttpStatusCode.NotFound : HttpStatusCode.OK);

            return Task.FromResult(new HttpResponseMessage(estado)
            {
                Content = new StringContent(cuerpo, Encoding.UTF8, "application/json")
            });
        }
    }
}
