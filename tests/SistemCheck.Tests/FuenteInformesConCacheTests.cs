using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SistemCheck.Web.Datos;
using SistemCheck.Web.Dominio;
using SistemCheck.Web.Servicios;

namespace SistemCheck.Tests;

public sealed class FuenteInformesConCacheTests : IDisposable
{
    private const string Cuit = "30500010912";

    private readonly SqliteConnection _conexion = new("Data Source=:memory:");
    private readonly AppDbContext _db;
    private readonly RelojFijo _reloj = new(new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero));
    private readonly FuenteFalsa _interna;
    private readonly FuenteInformesConCache _fuente;

    public FuenteInformesConCacheTests()
    {
        _conexion.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conexion).Options);
        _db.Database.EnsureCreated();
        _interna = new FuenteFalsa(_reloj);
        _fuente = new FuenteInformesConCache(_interna, _db, _reloj);
    }

    [Fact]
    public async Task Primera_consulta_va_a_la_fuente_interna()
    {
        var resultado = await _fuente.ObtenerAsync(Cuit);

        Assert.False(resultado.DesdeCache);
        Assert.Equal(1, _interna.Llamadas);
    }

    [Fact]
    public async Task Dentro_de_las_24_horas_responde_desde_cache_con_los_mismos_datos()
    {
        var original = await _fuente.ObtenerAsync(Cuit);
        _reloj.Avanzar(TimeSpan.FromHours(23) + TimeSpan.FromMinutes(59));

        var resultado = await _fuente.ObtenerAsync(Cuit);

        Assert.True(resultado.DesdeCache);
        Assert.Equal(1, _interna.Llamadas);
        Assert.Equal(original.ConsultadoEnUtc, resultado.ConsultadoEnUtc);
        Assert.Equal(original.Informe.DeudaTotal, resultado.Informe.DeudaTotal);
        Assert.Equal(original.Informe.PeorSituacion, resultado.Informe.PeorSituacion);
        Assert.Equal(original.Informe.Denominacion, resultado.Informe.Denominacion);
    }

    [Fact]
    public async Task A_las_24_horas_vuelve_a_consultar_y_renueva_el_cache()
    {
        await _fuente.ObtenerAsync(Cuit);
        _reloj.Avanzar(TimeSpan.FromHours(24));

        var renovado = await _fuente.ObtenerAsync(Cuit);
        var siguiente = await _fuente.ObtenerAsync(Cuit);

        Assert.False(renovado.DesdeCache);
        Assert.True(siguiente.DesdeCache);
        Assert.Equal(2, _interna.Llamadas);
    }

    [Fact]
    public async Task Cada_cuit_tiene_su_propia_entrada()
    {
        await _fuente.ObtenerAsync(Cuit);
        await _fuente.ObtenerAsync("20123456786");

        Assert.Equal(2, _interna.Llamadas);
    }

    [Fact]
    public async Task Un_error_del_bcra_no_queda_cacheado()
    {
        _interna.Falla = true;
        await Assert.ThrowsAsync<BcraNoDisponibleException>(() => _fuente.ObtenerAsync(Cuit));

        _interna.Falla = false;
        var resultado = await _fuente.ObtenerAsync(Cuit);

        Assert.False(resultado.DesdeCache);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conexion.Dispose();
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        private DateTimeOffset _ahora = ahora;
        public override DateTimeOffset GetUtcNow() => _ahora;
        public void Avanzar(TimeSpan lapso) => _ahora += lapso;
    }

    private sealed class FuenteFalsa(TimeProvider reloj) : IFuenteInformes
    {
        public int Llamadas { get; private set; }
        public bool Falla { get; set; }

        public Task<InformeObtenido> ObtenerAsync(string cuit, CancellationToken ct = default)
        {
            Llamadas++;
            if (Falla) throw new BcraNoDisponibleException("caído");

            var informe = new InformeBcra(cuit, "Cliente", "202609", [new DeudaEntidad("Banco", 2, 5000m, 40)], 0, 0, 0m);
            return Task.FromResult(new InformeObtenido(informe, reloj.GetUtcNow().UtcDateTime, DesdeCache: false));
        }
    }
}
