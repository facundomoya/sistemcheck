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

public record DeudaEntidad(string Entidad, int Situacion, decimal Monto, int? DiasAtraso);
