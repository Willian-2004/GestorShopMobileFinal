using CommunityToolkit.Maui;
using GestorShopMobile.Services;

namespace GestorShopMobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit();
        // Nota: se quitó el registro de "OpenSans-Regular.ttf" porque el
        // archivo de fuente no existe en Resources/Fonts del proyecto.

        // AuthService es singleton: es la única fuente de verdad del estado
        // de sesión, compartida por todas las páginas. Esto resuelve el
        // requisito de "manejo de estado" y "persistencia durante la
        // navegación" sin duplicar el token o el usuario en cada página.
        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<ApiService>();
        builder.Services.AddSingleton<PermissionService>();

        builder.Services.AddTransient<Views.LoginPage>();
        builder.Services.AddTransient<Views.ProductosPage>();
        builder.Services.AddTransient<Views.RegistrarVentaPage>();
        builder.Services.AddTransient<Views.PerfilPage>();
        builder.Services.AddTransient<Views.HistorialVentasPage>();
        builder.Services.AddTransient<Views.NuevoProductoPage>();

        return builder.Build();
    }
}
