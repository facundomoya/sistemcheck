using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using SistemCheck.Web.Datos;
using SistemCheck.Web.Servicios;

// Va antes de CreateBuilder porque la configuración lee las variables de entorno al crearse.
// Busca el .env hacia arriba: así funciona tanto con dotnet run como desde Visual Studio.
DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// dotnet run solo lo activa en Development. Sin esto, el perfil tunel (Production) no encuentra los
// CSS y JS comprimidos ni los estilos de las páginas, y el sitio se ve sin diseño.
builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Cuenta/Ingresar");
    options.Conventions.AllowAnonymousToPage("/Cuenta/Registrarse");
    options.Conventions.AllowAnonymousToPage("/Error");
});
builder.Services.AddControllers();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SistemCheck API",
        Version = "v1",
        Description = "Evaluación de riesgo comercial a partir de la Central de Deudores del BCRA. " +
                      "Pública, sin autenticación. Los errores siguen el formato ProblemDetails (RFC 7807)."
    });
    // Toma los comentarios /// del código, que se generan con GenerateDocumentationFile en el .csproj.
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "SistemCheck.Web.xml"));
});

// Errores de la API en formato ProblemDetails (RFC 7807), sin detalles internos del servidor.
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = contexto =>
{
    contexto.ProblemDetails.Detail ??= contexto.ProblemDetails.Status switch
    {
        StatusCodes.Status404NotFound => "El recurso solicitado no existe.",
        StatusCodes.Status429TooManyRequests => "Se superó el límite de consultas por minuto. Intente nuevamente más tarde.",
        StatusCodes.Status500InternalServerError => "Ocurrió un error inesperado. Intente nuevamente más tarde.",
        _ => null
    };
});

// CORS para aplicaciones web de otros grupos. Sin orígenes configurados, no se permite ninguno.
var origenesPermitidos = builder.Configuration.GetSection("Cors:OrigenesPermitidos").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(PoliticaCors, politica => politica
    .WithOrigins(origenesPermitidos)
    .WithMethods("GET")
    .AllowAnyHeader()));

// La API es pública y cada consulta escribe en la base: se limita por IP para evitar abusos.
var limitePorMinuto = builder.Configuration.GetValue("Api:LimitePorMinuto", 60);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(PoliticaLimite, contexto => RateLimitPartition.GetFixedWindowLimiter(
        contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = limitePorMinuto, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => options.LoginPath = "/Cuenta/Ingresar");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySQL(builder.Configuration.GetConnectionString("Default")!));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<EvaluacionService>();

builder.Services.AddHttpClient<BcraApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Bcra:BaseUrl"]!);
    client.Timeout = TimeSpan.FromSeconds(15);
});

// Patrón Decorator: quien pide un IFuenteInformes recibe el caché envolviendo a la API real.
builder.Services.AddScoped<IFuenteInformes>(sp => new FuenteInformesConCache(
    sp.GetRequiredService<BcraApiClient>(),
    sp.GetRequiredService<AppDbContext>(),
    sp.GetRequiredService<TimeProvider>()));

var app = builder.Build();

// Los tests de la API lo desactivan para usar SQLite en lugar de MySQL.
if (app.Configuration.GetValue("BaseDeDatos:MigrarAlIniciar", true))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// En /api, cualquier error (excepciones, 404, 405) responde ProblemDetails en JSON, también en
// Development: los consumidores nunca ven la página de error de desarrollo ni el stack trace.
app.UseWhen(contexto => contexto.Request.Path.StartsWithSegments("/api"), api =>
{
    api.UseExceptionHandler();
    api.UseStatusCodePages();
});

// La documentación está siempre disponible para que otros grupos puedan consultarla.
app.UseSwagger();
app.UseSwaggerUI();

app.UseRouting();
app.UseCors();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllers()
   .RequireCors(PoliticaCors)
   .RequireRateLimiting(PoliticaLimite);
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

public partial class Program
{
    private const string PoliticaCors = "Integraciones";
    private const string PoliticaLimite = "PorIp";
}
