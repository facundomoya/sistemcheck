namespace SistemCheck.Web.Dominio;

/// <summary>Datos del deudor en la Central de Deudores. Los montos están en pesos.</summary>
public record InformeBcra(
    string Cuit,
    string? Denominacion,
    string? Periodo,
    List<DeudaEntidad> Deudas,
    int ChequesRechazados,
    int ChequesImpagos,
    decimal MontoChequesImpagos)
{
    /// <summary>Null cuando el BCRA no informa deudas para el CUIT.</summary>
    public int? PeorSituacion => Deudas.Count == 0 ? null : Deudas.Max(d => d.Situacion);

    public decimal DeudaTotal => Deudas.Sum(d => d.Monto);
}

/// <summary>Deuda de un CUIT con una entidad financiera, en el último período informado.</summary>
/// <param name="Entidad">Nombre de la entidad financiera.</param>
/// <param name="Situacion">Situación crediticia informada por la entidad (1 a 5).</param>
/// <param name="Monto">Monto adeudado, en pesos.</param>
/// <param name="DiasAtraso">Días de atraso en el pago. Null si la entidad no lo informa.</param>
public record DeudaEntidad(string Entidad, int Situacion, decimal Monto, int? DiasAtraso);
