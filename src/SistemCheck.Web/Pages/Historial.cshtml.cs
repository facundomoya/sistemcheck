using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemCheck.Web.Datos;

namespace SistemCheck.Web.Pages;

public class HistorialModel(AppDbContext db) : PageModel
{
    public List<Consulta> Consultas { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var usuarioId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        Consultas = await db.Consultas
            .AsNoTracking()
            .Where(c => c.UsuarioId == usuarioId)
            .OrderByDescending(c => c.FechaUtc)
            .Take(100)
            .ToListAsync(ct);
    }
}
