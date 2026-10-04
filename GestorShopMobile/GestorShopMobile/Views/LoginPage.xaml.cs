using GestorShopMobile.Services;

namespace GestorShopMobile.Views;

public partial class LoginPage : ContentPage
{
    private readonly ApiService _api;
    private readonly AuthService _auth;

    public LoginPage(ApiService api, AuthService auth)
    {
        InitializeComponent();
        _api = api;
        _auth = auth;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        MostrarServidor();
    }

    private void MostrarServidor() => LblServidor.Text = $"Servidor: {AppConfig.ApiBaseUrl}";

    private async void OnConfigurarServidorClicked(object sender, EventArgs e)
    {
        var url = await DisplayPromptAsync(
            "Servidor",
            $"URL de GestorShopAPI.\nEmulador: {AppConfig.UrlPorDefecto}\nDeja vacío para restablecer el valor por defecto.",
            "Guardar", "Cancelar",
            initialValue: AppConfig.ApiBaseUrl,
            keyboard: Keyboard.Url);

        if (url is null) return; // canceló

        if (string.IsNullOrWhiteSpace(url))
        {
            AppConfig.Restablecer();
        }
        else if (Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
                 && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            AppConfig.ApiBaseUrl = url.Trim();
        }
        else
        {
            await DisplayAlert("URL inválida", "Ingresa una URL completa, por ejemplo http://10.0.2.2:5164", "OK");
            return;
        }

        MostrarServidor();
    }

    private async void OnIngresarClicked(object sender, EventArgs e)
    {
        if (!ValidarFormulario())
            return;

        LblErrorGeneral.IsVisible = false;
        Cargando.IsRunning = true;
        Cargando.IsVisible = true;
        BtnIngresar.IsEnabled = false;

        var usuario = EntryUsuario.Text.Trim();
        var (ok, datos, error) = await _api.LoginAsync(usuario, EntryPassword.Text);

        Cargando.IsRunning = false;
        Cargando.IsVisible = false;
        BtnIngresar.IsEnabled = true;

        if (!ok || datos is null)
        {
            LblErrorGeneral.Text = error ?? "No se pudo iniciar sesión.";
            LblErrorGeneral.IsVisible = true;
            return;
        }

        await _auth.IniciarSesionAsync(datos.Token, usuario, datos.Rol);
        EntryPassword.Text = string.Empty; // no dejar la contraseña en pantalla tras cerrar sesión
        await Shell.Current.GoToAsync("//app/productos");
    }

    // Validación de campos obligatorios y formato mínimo, con mensajes
    // comprensibles por campo (requisito explícito del taller).
    private bool ValidarFormulario()
    {
        var valido = true;

        if (string.IsNullOrWhiteSpace(EntryUsuario.Text))
        {
            LblErrorUsuario.Text = "El usuario es obligatorio.";
            LblErrorUsuario.IsVisible = true;
            valido = false;
        }
        else
        {
            LblErrorUsuario.IsVisible = false;
        }

        if (string.IsNullOrWhiteSpace(EntryPassword.Text) || EntryPassword.Text.Length < 4)
        {
            LblErrorPassword.Text = "La contraseña debe tener al menos 4 caracteres.";
            LblErrorPassword.IsVisible = true;
            valido = false;
        }
        else
        {
            LblErrorPassword.IsVisible = false;
        }

        return valido;
    }
}
