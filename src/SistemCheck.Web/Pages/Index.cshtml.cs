using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SistemCheck.Web.Dominio;
using SistemCheck.Web.Servicios;

namespace SistemCheck.Web.Pages;

public class IndexModel(EvaluacionService evaluaciones, ILogger<IndexModel> logger) : PageModel
{
    [BindProperty, Required(ErrorMessage = "Ingresá el CUIT/CUIL."), Display(Name = "CUIT/CUIL")]
    public string? Cuit { get; set; }

    public Evaluacion? Resultado { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        string cuit = "";
        if (Cuit is not null && !Dominio.Cuit.TryNormalizar(Cuit, out cuit))
            ModelState.AddModelError(nameof(Cuit), "El CUIT/CUIL no es válido: revisá los 11 dígitos.");

        if (!ModelState.IsValid)
            return Page();

        try
        {
            var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            Resultado = await evaluaciones.EvaluarAsync(cuit, usuarioId, ct);
        }
        catch (BcraNoDisponibleException ex)
        {
            logger.LogWarning(ex, "No se pudo obtener el informe del BCRA.");
            ModelState.AddModelError("", "El servicio del BCRA no está disponible en este momento. Intentá nuevamente en unos minutos.");
        }

        return Page();
    }
}
