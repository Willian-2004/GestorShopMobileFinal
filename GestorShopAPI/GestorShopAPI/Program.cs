using System.Text;
using System.Text.Json.Serialization;
using GestorShopAPI.Data;
using GestorShopAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// --- URL fija del servidor ---
// La app móvil (emulador) espera la API en http://10.0.2.2:5164, que es el
// "localhost" del PC visto desde el emulador. Sin esta línea (y sin
// launchSettings.json) "dotnet run" levanta en el puerto 5000 y la app no
// encuentra el servidor. Se puede sobrescribir con la clave "Urls" de
// appsettings.json o con la variable de entorno ASPNETCORE_URLS.
if (string.IsNullOrEmpty(builder.Configuration["Urls"]) &&
    string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    builder.WebHost.UseUrls("http://localhost:5164");
}

// --- Base de datos (SQLite) con lazy loading habilitado vía proxies ---
// IgnoreNonVirtualNavigations(): sin esta opción EF Core lanza una excepción
// al arrancar porque Venta.Detalles, DetalleVenta.Venta y
// DetalleVenta.Producto NO son "virtual" (decisión de diseño: esas
// relaciones se cargan con eager loading). Con la opción, solo las
// navegaciones virtual (Producto.Categoria, Categoria.Productos) usan lazy
// loading y el resto se respeta tal como está.
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseLazyLoadingProxies(o => o.IgnoreNonVirtualNavigations())
       .UseSqlite(builder.Configuration.GetConnectionString("Default")
                  ?? "Data Source=gestorshop.db"));

// --- Caché (cache-aside) ---
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ICacheService, CacheService>();

// --- Cola de trabajo asíncrona (worker en segundo plano) ---
builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
builder.Services.AddHostedService<QueuedHostedService>();

// --- JWT ---
// La clave NO está en el código ni en appsettings.json (que sí se sube a
// GitHub): se lee de appsettings.Development.json (excluido del repo por
// .gitignore) o de la variable de entorno Jwt__Key en producción.
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
    throw new InvalidOperationException(
        "Falta la clave JWT (Jwt:Key, mínimo 32 caracteres). Copie " +
        "appsettings.Development.example.json como appsettings.Development.json " +
        "y complete los valores, o defina la variable de entorno Jwt__Key.");

builder.Services.AddSingleton<JwtService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey))
        };
        // No se requiere ninguna consulta a BD aquí: el rol viaja en las
        // claims del propio token (ver JwtService), por eso la validación
        // no genera consultas redundantes en cada petición protegida.
    });
builder.Services.AddAuthorization();

// IgnoreCycles: evita el error 500 "A possible object cycle was detected"
// al serializar entidades con referencias circulares (Venta <-> DetalleVenta,
// Producto <-> Categoria con lazy loading).
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    SeedData.Inicializar(db, app.Configuration["Seed:AdminPassword"], app.Logger);
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
