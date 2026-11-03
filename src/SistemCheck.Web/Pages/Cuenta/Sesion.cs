using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using SistemCheck.Web.Datos;

namespace SistemCheck.Web.Pages.Cuenta;

internal static class Sesion
{
    public static Task IniciarAsync(HttpContext http, Usuario usuario)
    {
        var identidad = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new Claim(ClaimTypes.Name, usuario.Empresa)
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);

        return http.SignInAsync(new ClaimsPrincipal(identidad));
    }
}
