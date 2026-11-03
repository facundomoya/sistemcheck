namespace SistemCheck.Web.Dominio;

public enum Semaforo
{
    Verde,
    Amarillo,
    Rojo
}

public record Dictamen(Semaforo Semaforo, string Resultado, string Resumen);

/// <summary>Reglas de negocio: traduce la peor situación crediticia a un semáforo.</summary>
public static class EvaluadorRiesgo
{
    public static Dictamen Evaluar(InformeBcra informe)
    {
        var dictamen = informe.PeorSituacion switch
        {
            null => new Dictamen(Semaforo.Amarillo, "PRECAUCIÓN",
                "Sin deudas informadas en el sistema financiero: no hay historial crediticio para respaldar la operación."),
            1 => new Dictamen(Semaforo.Verde, "APROBADO",
                "Situación 1: cliente sin mora o con atrasos menores a 31 días."),
            2 => new Dictamen(Semaforo.Amarillo, "PRECAUCIÓN",
                "Situación 2: registra atrasos de 31 a 90 días."),
            var situacion => new Dictamen(Semaforo.Rojo, "RECHAZADO",
                $"Situación {situacion}: alto riesgo o deuda incobrable.")
        };

        return informe.ChequesImpagos > 0
            ? dictamen with { Resumen = $"{dictamen.Resumen} Atención: tiene {informe.ChequesImpagos} cheque(s) rechazado(s) sin pagar." }
            : dictamen;
    }
}
