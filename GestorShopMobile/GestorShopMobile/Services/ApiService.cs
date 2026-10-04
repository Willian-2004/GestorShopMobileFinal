using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GestorShopMobile.Models;

namespace GestorShopMobile.Services;

public class ApiService
{
    // Tiempo máximo de espera por petición. Sin esto HttpClient espera 100 s
    // por defecto y, si el servidor no responde, la app parece "congelada".
    private static readonly TimeSpan TiempoEspera = TimeSpan.FromSeconds(15);

    private readonly AuthService _auth;

    public ApiService(AuthService auth)
    {
        _auth = auth;
    }

    private HttpClient CrearCliente(bool conToken = true)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(NormalizarUrl(AppConfig.ApiBaseUrl)),
            Timeout = TiempoEspera
        };
        if (conToken && !string.IsNullOrEmpty(_auth.Token))
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _auth.Token);
        return client;
    }

    // HttpClient necesita que la BaseAddress termine en "/" para que las
    // rutas relativas ("api/...") se concatenen correctamente.
    private static string NormalizarUrl(string url) =>
        url.Trim().EndsWith('/') ? url.Trim() : url.Trim() + "/";

    // Traduce las excepciones de red a mensajes comprensibles para el usuario.
    private static string MensajeConexion(Exception ex) => ex switch
    {
        TaskCanceledException =>
            $"El servidor no respondió a tiempo ({AppConfig.ApiBaseUrl}). Verifica que GestorShopAPI esté en ejecución.",
        HttpRequestException =>
            $"No se pudo conectar con el servidor ({AppConfig.ApiBaseUrl}). Verifica que GestorShopAPI esté en ejecución y que la URL sea correcta.",
        UriFormatException =>
            $"La URL del servidor no es válida: {AppConfig.ApiBaseUrl}",
        _ => $"Error de conexión: {ex.Message}"
    };

    public async Task<(bool ok, LoginResponse? datos, string? error)> LoginAsync(string username, string password)
    {
        try
        {
            using var client = CrearCliente(conToken: false);
            var resp = await client.PostAsJsonAsync("api/auth/login", new LoginRequest(username, password));

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
                return (false, null, "Usuario o contraseña incorrectos.");

            if (!resp.IsSuccessStatusCode)
                return (false, null, $"El servidor respondió con un error ({(int)resp.StatusCode}).");

            var datos = await resp.Content.ReadFromJsonAsync<LoginResponse>();
            if (datos is null || string.IsNullOrEmpty(datos.Token))
                return (false, null, "Respuesta inválida del servidor.");

            return (true, datos, null);
        }
        catch (Exception ex)
        {
            return (false, null, MensajeConexion(ex));
        }
    }

    public async Task<(bool ok, ProductosResponse? datos, string? error)> ObtenerProductosAsync()
    {
        try
        {
            using var client = CrearCliente();
            var resp = await client.GetAsync("api/productos");
            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                _auth.CerrarSesion();
                return (false, null, "Tu sesión expiró. Vuelve a iniciar sesión.");
            }
            if (!resp.IsSuccessStatusCode) return (false, null, "No se pudo obtener el catálogo.");
            var datos = await resp.Content.ReadFromJsonAsync<ProductosResponse>();
            return (true, datos, null);
        }
        catch (Exception ex)
        {
            return (false, null, MensajeConexion(ex));
        }
    }

    public async Task<(bool ok, string? error)> RegistrarVentaAsync(NuevaVentaRequest venta)
    {
        try
        {
            using var client = CrearCliente();
            var resp = await client.PostAsJsonAsync("api/ventas", venta);

            // El token JWT dura 15 minutos: si expiró, se cierra la sesión
            // local para que la app vuelva al login.
            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                _auth.CerrarSesion();
                return (false, "Tu sesión expiró. Vuelve a iniciar sesión.");
            }

            if (!resp.IsSuccessStatusCode)
            {
                var detalle = await LeerMensajeAsync(resp);
                return (false, detalle ?? "El servidor rechazó la venta.");
            }
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, MensajeConexion(ex));
        }
    }

    public async Task<(bool ok, List<VentaDto>? datos, string? error)> ObtenerVentasAsync()
    {
        try
        {
            using var client = CrearCliente();
            var resp = await client.GetAsync("api/ventas");
            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                _auth.CerrarSesion();
                return (false, null, "Tu sesión expiró. Vuelve a iniciar sesión.");
            }
            if (!resp.IsSuccessStatusCode) return (false, null, "No se pudo obtener el historial de ventas.");
            var datos = await resp.Content.ReadFromJsonAsync<List<VentaDto>>();
            return (true, datos ?? new List<VentaDto>(), null);
        }
        catch (Exception ex)
        {
            return (false, null, MensajeConexion(ex));
        }
    }

    public async Task<(bool ok, List<CategoriaDto>? datos, string? error)> ObtenerCategoriasAsync()
    {
        try
        {
            using var client = CrearCliente();
            var resp = await client.GetAsync("api/categorias");
            if (resp.StatusCode == HttpStatusCode.NotFound)
                return (false, null, "La API no tiene la lista de categorías. Reinicia GestorShopAPI (Ctrl+C y dotnet run).");
            if (!resp.IsSuccessStatusCode) return (false, null, "No se pudieron obtener las categorías.");
            var datos = await resp.Content.ReadFromJsonAsync<List<CategoriaDto>>();
            return (true, datos ?? new List<CategoriaDto>(), null);
        }
        catch (Exception ex)
        {
            return (false, null, MensajeConexion(ex));
        }
    }

    public async Task<(bool ok, CategoriaDto? categoria, string? error)> CrearCategoriaAsync(string nombre)
    {
        try
        {
            using var client = CrearCliente();
            var resp = await client.PostAsJsonAsync("api/categorias", new NuevaCategoriaRequest(nombre));

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                _auth.CerrarSesion();
                return (false, null, "Tu sesión expiró. Vuelve a iniciar sesión.");
            }
            if (resp.StatusCode == HttpStatusCode.Forbidden)
                return (false, null, "Solo un Administrador puede crear categorías.");
            if (resp.StatusCode == HttpStatusCode.NotFound || resp.StatusCode == HttpStatusCode.MethodNotAllowed)
                return (false, null, "La API no tiene esta función. Reinicia GestorShopAPI (Ctrl+C y dotnet run).");

            if (!resp.IsSuccessStatusCode)
            {
                var detalle = await LeerMensajeAsync(resp);
                return (false, null, detalle ?? "El servidor rechazó la categoría.");
            }

            var categoria = await resp.Content.ReadFromJsonAsync<CategoriaDto>();
            return (true, categoria, null);
        }
        catch (Exception ex)
        {
            return (false, null, MensajeConexion(ex));
        }
    }

    public async Task<(bool ok, string? error)> CrearProductoAsync(NuevoProductoRequest producto)
    {
        try
        {
            using var client = CrearCliente();
            var resp = await client.PostAsJsonAsync("api/productos", producto);

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                _auth.CerrarSesion();
                return (false, "Tu sesión expiró. Vuelve a iniciar sesión.");
            }
            if (resp.StatusCode == HttpStatusCode.Forbidden)
                return (false, "Solo un Administrador puede agregar productos.");

            if (!resp.IsSuccessStatusCode)
            {
                var detalle = await LeerMensajeAsync(resp);
                return (false, detalle ?? "El servidor rechazó el producto.");
            }
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, MensajeConexion(ex));
        }
    }

    public async Task<(bool ok, ProductoDetalleDto? datos, string? error)> ObtenerProductoAsync(int id)
    {
        try
        {
            using var client = CrearCliente();
            var resp = await client.GetAsync($"api/productos/{id}");
            if (resp.StatusCode == HttpStatusCode.NotFound)
                return (false, null, "El producto ya no existe. Pudo haber sido eliminado.");
            if (!resp.IsSuccessStatusCode) return (false, null, "No se pudo obtener el producto.");
            var datos = await resp.Content.ReadFromJsonAsync<ProductoDetalleDto>();
            return (true, datos, null);
        }
        catch (Exception ex)
        {
            return (false, null, MensajeConexion(ex));
        }
    }

    public async Task<(bool ok, string? error)> ActualizarProductoAsync(int id, NuevoProductoRequest producto)
    {
        try
        {
            using var client = CrearCliente();
            var resp = await client.PutAsJsonAsync($"api/productos/{id}", producto);
            return await InterpretarEscrituraAsync(resp, "El servidor rechazó los cambios.");
        }
        catch (Exception ex)
        {
            return (false, MensajeConexion(ex));
        }
    }

    public async Task<(bool ok, string? error)> EliminarProductoAsync(int id)
    {
        try
        {
            using var client = CrearCliente();
            var resp = await client.DeleteAsync($"api/productos/{id}");
            return await InterpretarEscrituraAsync(resp, "El servidor no permitió eliminar el producto.");
        }
        catch (Exception ex)
        {
            return (false, MensajeConexion(ex));
        }
    }

    // Traduce los códigos HTTP de una operación de escritura (PUT/DELETE)
    // a un resultado comprensible para el usuario.
    private async Task<(bool ok, string? error)> InterpretarEscrituraAsync(HttpResponseMessage resp, string mensajePorDefecto)
    {
        if (resp.IsSuccessStatusCode) return (true, null);

        switch (resp.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
                _auth.CerrarSesion();
                return (false, "Tu sesión expiró. Vuelve a iniciar sesión.");
            case HttpStatusCode.Forbidden:
                return (false, "Solo un Administrador puede modificar productos.");
            case HttpStatusCode.MethodNotAllowed:
                return (false, "La API no tiene esta función. Reinicia GestorShopAPI (Ctrl+C y dotnet run).");
        }

        // 400 (validación), 404 (no existe) y 409 (conflicto) traen "mensaje"
        var detalle = await LeerMensajeAsync(resp);
        return (false, detalle ?? mensajePorDefecto);
    }

    // Lee el campo "mensaje" que devuelve la API en los errores 400.
    private static async Task<string?> LeerMensajeAsync(HttpResponseMessage resp)
    {
        try
        {
            var cuerpo = await resp.Content.ReadFromJsonAsync<RespuestaError>();
            return cuerpo?.Mensaje;
        }
        catch
        {
            return null;
        }
    }

    private sealed class RespuestaError
    {
        public string? Mensaje { get; set; }
    }
}
