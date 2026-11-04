using SistemCheck.Web.Dominio;

namespace SistemCheck.Web.Datos;

public class Usuario
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public required string Empresa { get; set; }
    public string PasswordHash { get; set; } = "";
}

/// <summary>Última respuesta del BCRA para un CUIT, serializada como JSON.</summary>
public class InformeCacheado
{
    public required string Cuit { get; set; }
    public required string Json { get; set; }
    public DateTime ConsultadoEnUtc { get; set; }
}

public class Consulta
{
    public int Id { get; set; }

    /// <summary>Null cuando la consulta llega por la API REST.</summary>
    public int? UsuarioId { get; set; }

    public required string Cuit { get; set; }
    public string? Denominacion { get; set; }
    public Semaforo Semaforo { get; set; }
    public int? PeorSituacion { get; set; }
    public decimal DeudaTotal { get; set; }
    public DateTime FechaUtc { get; set; }
}
