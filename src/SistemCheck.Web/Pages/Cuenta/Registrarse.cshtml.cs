using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SistemCheck.Web.Datos;

namespace SistemCheck.Web.Pages.Cuenta;

public class RegistrarseModel(AppDbContext db) : PageModel
{
    [BindProperty, Required(ErrorMessage = "Ingresá el nombre de la empresa."), StringLength(100), Display(Name = "Empresa")]
    public string Empresa { get; set; } = "";

    [BindProperty, Required(ErrorMessage = "Ingresá tu email."), EmailAddress(ErrorMessage = "El email no es válido."), Display(Name = "Email")]
    public string Email { get; set; } = "";

    [BindProperty, Required(ErrorMessage = "Ingresá una contraseña."), DataType(DataType.Password), Display(Name = "Contraseña")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    public string Password { get; set; } = "";

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var email = Email.Trim().ToLowerInvariant();

        if (ModelState.IsValid && await db.Usuarios.AnyAsync(u => u.Email == email, ct))
            ModelState.AddModelError(nameof(Email), "Ya existe una cuenta con ese email.");

        if (!ModelState.IsValid)
            return Page();

        var usuario = new Usuario { Email = email, Empresa = Empresa.Trim() };
        usuario.PasswordHash = new PasswordHasher<Usuario>().HashPassword(usuario, Password);

        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync(ct);

        await Sesion.IniciarAsync(HttpContext, usuario);
        return RedirectToPage("/Index");
    }
}
