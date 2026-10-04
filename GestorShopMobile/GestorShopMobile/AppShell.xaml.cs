using GestorShopMobile.Services;

namespace GestorShopMobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Pantallas de detalle (se apilan sobre la pestaña actual, con botón
        // de "atrás"): no forman parte del TabBar.
        Routing.RegisterRoute("nuevo-producto", typeof(Views.NuevoProductoPage));

        // Al arrancar, intenta restaurar la sesión desde SecureStorage
        // (persistencia local del token) y decide la ruta inicial.
        Loaded += async (_, _) =>
        {
            var auth = IPlatformApplication.Current!.Services.GetRequiredService<AuthService>();
            await auth.RestaurarSesionAsync();

            if (auth.IsAuthenticated)
                await GoToAsync("//app/productos");
            else
                await GoToAsync("//login");
        };
    }
}
