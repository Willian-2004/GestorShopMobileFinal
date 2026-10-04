namespace GestorShopMobile;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    // En .NET MAUI 9/10 la propiedad MainPage está obsoleta: la página
    // inicial se entrega creando la ventana principal.
    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}
