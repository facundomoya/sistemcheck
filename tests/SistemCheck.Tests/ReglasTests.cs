using SistemCheck.Web.Dominio;

namespace SistemCheck.Tests;

public class ReglasTests
{
    [Theory]
    [InlineData("30500010912")]
    [InlineData("30-50001091-2")]
    [InlineData(" 20-12345678-6 ")]
    public void Cuit_valido_se_normaliza(string entrada)
    {
        Assert.True(Cuit.TryNormalizar(entrada, out var cuit));
        Assert.Equal(11, cuit.Length);
        Assert.All(cuit, c => Assert.True(char.IsAsciiDigit(c)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("30500010913")] // dígito verificador incorrecto
    [InlineData("3050001091a")]
    [InlineData("٣0500010912")] // dígito no ASCII
    public void Cuit_invalido_se_rechaza(string? entrada)
    {
        Assert.False(Cuit.TryNormalizar(entrada, out _));
    }

    [Theory]
    [InlineData(1, Semaforo.Verde, "APROBADO")]
    [InlineData(2, Semaforo.Amarillo, "PRECAUCIÓN")]
    [InlineData(3, Semaforo.Rojo, "RECHAZADO")]
    [InlineData(4, Semaforo.Rojo, "RECHAZADO")]
    [InlineData(5, Semaforo.Rojo, "RECHAZADO")]
    [InlineData(6, Semaforo.Rojo, "RECHAZADO")]
    public void La_situacion_define_el_semaforo(int situacion, Semaforo esperado, string resultado)
    {
        var dictamen = EvaluadorRiesgo.Evaluar(Informe(situacion));

        Assert.Equal(esperado, dictamen.Semaforo);
        Assert.Equal(resultado, dictamen.Resultado);
    }

    [Fact]
    public void Manda_la_peor_situacion_entre_entidades()
    {
        var dictamen = EvaluadorRiesgo.Evaluar(Informe(1, 1, 4));

        Assert.Equal(Semaforo.Rojo, dictamen.Semaforo);
    }

    [Fact]
    public void Sin_deudas_informadas_es_precaucion()
    {
        var dictamen = EvaluadorRiesgo.Evaluar(Informe());

        Assert.Equal(Semaforo.Amarillo, dictamen.Semaforo);
    }

    [Fact]
    public void Cheques_impagos_avisan_pero_no_cambian_el_semaforo()
    {
        var dictamen = EvaluadorRiesgo.Evaluar(Informe(1) with { ChequesRechazados = 2, ChequesImpagos = 2 });

        Assert.Equal(Semaforo.Verde, dictamen.Semaforo);
        Assert.Contains("2 cheque(s)", dictamen.Resumen);
    }

    private static InformeBcra Informe(params int[] situaciones) => new(
        "30500010912", "Cliente", "202609",
        situaciones.Select(s => new DeudaEntidad("Banco", s, 1000m, 0)).ToList(),
        0, 0, 0m);
}
