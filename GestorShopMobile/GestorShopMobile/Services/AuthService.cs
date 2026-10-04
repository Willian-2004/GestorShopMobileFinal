using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace GestorShopMobile.Services;

// Fuente única de verdad del estado de sesión. Registrado como singleton en
// MauiProgram, así que todas las páginas ven el mismo estado sin necesidad
// de pasarlo manualmente entre ellas (requisito: "manejo de estado" +
// "persistencia durante la navegación").
public class AuthService : INotifyPropertyChanged
{
    private const string ClaveToken = "auth_token";
    private const string ClaveUsuario = "auth_username";
    private const string ClaveRol = "auth_rol";

    public string? Token { get; private set; }
    public string? Username { get; private set; }
    public string? Rol { get; private set; }
    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);
    public bool EsAdministrador => Rol == "Administrador";

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? AuthStateChanged;

    // Se llama al iniciar la app: recupera el token de SecureStorage
    // (almacenamiento cifrado del dispositivo) para no forzar un nuevo
    // login cada vez que se abre la aplicación.
    public async Task RestaurarSesionAsync()
    {
        try
        {
            Token = await SecureStorage.GetAsync(ClaveToken);
            Username = await SecureStorage.GetAsync(ClaveUsuario);
            Rol = await SecureStorage.GetAsync(ClaveRol);
        }
        catch
        {
            // En Android, SecureStorage puede fallar tras reinstalar la app
            // (las claves de cifrado cambian). Se descarta la sesión guardada.
            Token = Username = Rol = null;
            SecureStorage.RemoveAll();
        }

        // El JWT del backend dura 15 minutos: si el token guardado ya
        // expiró, no se restaura la sesión y se pide un nuevo login.
        if (!string.IsNullOrEmpty(Token) && TokenExpirado(Token))
        {
            CerrarSesion();
            return;
        }

        Notificar();
    }

    // Lee el claim "exp" (segundos Unix) del payload del JWT sin librerías
    // externas. Si no se puede leer, se considera expirado por seguridad.
    private static bool TokenExpirado(string token)
    {
        try
        {
            var partes = token.Split('.');
            if (partes.Length < 2) return true;

            var payload = partes[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("exp", out var exp)) return true;

            var vence = DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64());
            return vence <= DateTimeOffset.UtcNow.AddSeconds(30);
        }
        catch
        {
            return true;
        }
    }

    public async Task IniciarSesionAsync(string token, string username, string rol)
    {
        Token = token;
        Username = username;
        Rol = rol;

        await SecureStorage.SetAsync(ClaveToken, token);
        await SecureStorage.SetAsync(ClaveUsuario, username);
        await SecureStorage.SetAsync(ClaveRol, rol);

        Notificar();
    }

    public void CerrarSesion()
    {
        Token = null;
        Username = null;
        Rol = null;
        SecureStorage.RemoveAll();
        Notificar();
    }

    private void Notificar()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        AuthStateChanged?.Invoke();
    }
}
