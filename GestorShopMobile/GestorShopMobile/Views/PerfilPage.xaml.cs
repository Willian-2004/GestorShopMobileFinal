using GestorShopMobile.Services;

namespace GestorShopMobile.Views;

public partial class PerfilPage : ContentPage
{
    private readonly AuthService _auth;

    public PerfilPage(AuthService auth)
    {
        InitializeComponent();
        _auth = auth;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_auth.IsAuthenticated)
        {
            await Shell.Current.GoToAsync("//login");
            return;
        }

        LblUsuario.Text = _auth.Username;
        LblRol.Text = $"Rol: {_auth.Rol}";
        EntryApiUrl.Text = AppConfig.ApiBaseUrl;
    }

    private async void OnGuardarUrlClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EntryApiUrl.Text) || !Uri.TryCreate(EntryApiUrl.Text, UriKind.Absolute, out _))
        {
            await DisplayAlert("URL inválida", "Ingresa una URL completa, por ejemplo http://192.168.1.100:5164", "OK");
            return;
        }

        AppConfig.ApiBaseUrl = EntryApiUrl.Text.Trim();
        await DisplayAlert("Listo", "La nueva URL se usará en las próximas peticiones.", "OK");
    }

    // Cierre de sesión: limpia el token persistido y las vistas protegidas
    // vuelven a redirigir al login en su próximo OnAppearing.
    private async void OnCerrarSesionClicked(object sender, EventArgs e)
    {
        _auth.CerrarSesion();
        await Shell.Current.GoToAsync("//login");
    }
}
