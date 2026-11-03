using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemCheck.Web.Datos;

namespace SistemCheck.Web.Pages.Cuenta;

public class IngresarModel(AppDbContext db) : PageModel
{
    [BindProperty, Required(ErrorMessage = "Ingresá tu email."), EmailAddress(ErrorMessage = "El email no es válido."), Display(Name = "Email")]
    public string Email { get; set; } = "";

    [BindProperty, Required(ErrorMessage = "Ingresá tu contraseña."), DataType(DataType.Password), Display(Name = "Contraseña")]
    public string Password { get; set; } = "";

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return Page();

        var email = Email.Trim().ToLowerInvariant();
        var usuario = await db.Usuarios.SingleOrDefaultAsync(u => u.Email == email, ct);

        if (usuario is null || new PasswordHasher<Usuario>().VerifyHashedPassword(usuario, usuario.PasswordHash, Password) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError("", "Email o contraseña incorrectos.");
            return Page();
        }

        await Sesion.IniciarAsync(HttpContext, usuario);
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
    }
}
