using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using SistemCheck.Web.Datos;
using SistemCheck.Web.Servicios;

// Va antes de CreateBuilder porque la configuración lee las variables de entorno al crearse.
// Busca el .env hacia arriba: así funciona tanto con dotnet run como desde Visual Studio.
DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

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

app.UseHttpsRedirection();

// Los formularios envían decimales con punto (input type="number"). Sin esto, en un servidor
// con cultura es-AR el binding leería "1500.50" como 150050. El formato en pantalla lo hace Formato.
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(CultureInfo.InvariantCulture),
    SupportedCultures = [CultureInfo.InvariantCulture],
    SupportedUICultures = [CultureInfo.InvariantCulture]
});

app.UseRouting();
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllers()
   .RequireCors(PoliticaCors);
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

public partial class Program
{
    private const string PoliticaCors = "Integraciones";
}
