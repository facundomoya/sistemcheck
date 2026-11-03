using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
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
builder.Services.AddSwaggerGen();

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

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

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

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllers();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
