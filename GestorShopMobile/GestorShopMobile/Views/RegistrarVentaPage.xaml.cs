using GestorShopMobile.Models;
using GestorShopMobile.Services;

namespace GestorShopMobile.Views;

public partial class RegistrarVentaPage : ContentPage
{
    private readonly ApiService _api;
    private readonly AuthService _auth;
    private readonly PermissionService _permisos;

    private List<ProductoDto> _productos = new();
    private string? _rutaFotoEvidencia; // persistida en Preferences (Semana 14: integración con almacenamiento local)
    private Location? _ubicacionEntrega;

    public RegistrarVentaPage(ApiService api, AuthService auth, PermissionService permisos)
    {
        InitializeComponent();
        _api = api;
        _auth = auth;
        _permisos = permisos;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_auth.IsAuthenticated)
        {
            await Shell.Current.GoToAsync("//login");
            return;
        }

        var seleccionPrevia = PickerProducto.SelectedIndex >= 0 && PickerProducto.SelectedIndex < _productos.Count
            ? _productos[PickerProducto.SelectedIndex].Id
            : (int?)null;

        var (ok, datos, error) = await _api.ObtenerProductosAsync();
        if (ok && datos is not null)
        {
            _productos = datos.Resultado;
            PickerProducto.ItemsSource = _productos
                .Select(p => $"{p.Nombre} (stock: {p.Stock})")
                .ToList();

            // Mantiene el producto elegido si el usuario cambió de pestaña y volvió.
            if (seleccionPrevia is not null)
                PickerProducto.SelectedIndex = _productos.FindIndex(p => p.Id == seleccionPrevia);
        }
        else
        {
            await DisplayAlert("Error", error ?? "No se pudo cargar la lista de productos.", "OK");
        }

        // Restaura evidencia de una venta anterior si el usuario navegó fuera
        // y volvió sin reiniciar la app (persistencia local simple).
        var rutaGuardada = Preferences.Get("ultima_evidencia_foto", string.Empty);
        if (!string.IsNullOrEmpty(rutaGuardada) && File.Exists(rutaGuardada))
        {
            _rutaFotoEvidencia = rutaGuardada;
            ImgEvidencia.Source = ImageSource.FromFile(rutaGuardada);
            ImgEvidencia.IsVisible = true;
        }
    }

    // ---------------------------------------------------------------
    // CAPACIDAD NATIVA 1: cámara (esencial para este flujo)
    // ---------------------------------------------------------------
    private async void OnTomarFotoClicked(object sender, EventArgs e)
    {
        BtnAjustes.IsVisible = false;

        // Explicación previa a la solicitud del permiso (requisito del taller).
        var continuar = await DisplayAlert(
            "Permiso de cámara",
            "GestorShop necesita la cámara para adjuntar una foto como evidencia de la entrega.",
            "Continuar", "Cancelar");
        if (!continuar) return;

        var resultado = await _permisos.SolicitarCamaraAsync();

        switch (resultado)
        {
            case ResultadoPermiso.Concedido:
                await CapturarFotoAsync();
                break;

            case ResultadoPermiso.DenegadoTemporal:
                LblEstadoFoto.Text = "Permiso denegado. Puedes intentarlo de nuevo cuando quieras.";
                LblEstadoFoto.TextColor = Colors.OrangeRed;
                break;

            case ResultadoPermiso.DenegadoPermanente:
                LblEstadoFoto.Text = "Permiso bloqueado permanentemente. Actívalo desde los ajustes del sistema.";
                LblEstadoFoto.TextColor = Colors.OrangeRed;
                BtnAjustes.IsVisible = true;
                break;
        }
    }

    private async Task CapturarFotoAsync()
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                LblEstadoFoto.Text = "Este dispositivo no tiene cámara disponible.";
                return;
            }

            var foto = await MediaPicker.Default.CapturePhotoAsync();
            if (foto is null) return; // el usuario canceló la captura

            var destino = Path.Combine(FileSystem.AppDataDirectory, $"evidencia_{DateTime.Now:yyyyMMddHHmmss}.jpg");
            using (var origen = await foto.OpenReadAsync())
            using (var archivoLocal = File.Create(destino))
                await origen.CopyToAsync(archivoLocal);

            _rutaFotoEvidencia = destino;
            Preferences.Set("ultima_evidencia_foto", destino); // persistencia local

            ImgEvidencia.Source = ImageSource.FromFile(destino);
            ImgEvidencia.IsVisible = true;
            LblEstadoFoto.Text = "Foto capturada correctamente.";
            LblEstadoFoto.TextColor = Colors.Green;
        }
        catch (Exception ex)
        {
            LblEstadoFoto.Text = $"No se pudo capturar la foto: {ex.Message}";
            LblEstadoFoto.TextColor = Colors.OrangeRed;
        }
    }

    // ---------------------------------------------------------------
    // CAPACIDAD NATIVA 2: geolocalización (opcional — degradación elegante)
    // ---------------------------------------------------------------
    private async void OnCapturarUbicacionClicked(object sender, EventArgs e)
    {
        BtnAjustes.IsVisible = false;

        var continuar = await DisplayAlert(
            "Permiso de ubicación",
            "GestorShop puede registrar el punto de entrega si compartes tu ubicación actual. Es opcional.",
            "Continuar", "Omitir");
        if (!continuar) return;

        var (resultado, ubicacion) = await _permisos.SolicitarUbicacionAsync();

        switch (resultado)
        {
            case ResultadoPermiso.Concedido:
                _ubicacionEntrega = ubicacion;
                LblEstadoUbicacion.Text = $"Ubicación capturada: {ubicacion!.Latitude:0.####}, {ubicacion.Longitude:0.####}";
                LblEstadoUbicacion.TextColor = Colors.Green;
                break;

            case ResultadoPermiso.DenegadoPermanente:
                LblEstadoUbicacion.Text = "Permiso bloqueado. La venta se puede registrar igual, sin coordenadas.";
                LblEstadoUbicacion.TextColor = Colors.OrangeRed;
                BtnAjustes.IsVisible = true;
                break;

            case ResultadoPermiso.NoDisponible:
                LblEstadoUbicacion.Text = "El GPS del dispositivo está apagado. La venta se registrará sin coordenadas.";
                LblEstadoUbicacion.TextColor = Colors.OrangeRed;
                break;

            default:
                LblEstadoUbicacion.Text = "No se obtuvo la ubicación. La venta se registrará sin coordenadas.";
                LblEstadoUbicacion.TextColor = Colors.OrangeRed;
                break;
        }
    }

    private void OnAbrirAjustesClicked(object sender, EventArgs e) => _permisos.AbrirAjustesDeLaApp();

    // Oculta el mensaje de error de un campo en cuanto el usuario lo corrige
    // (antes el mensaje quedaba visible hasta volver a pulsar "Registrar venta").
    private void OnCampoCambiado(object? sender, EventArgs e)
    {
        if (sender == EntryCliente) LblErrorCliente.IsVisible = false;
        else if (sender == EntryCantidad) LblErrorCantidad.IsVisible = false;
        else if (sender == PickerProducto) LblErrorProducto.IsVisible = false;
    }

    // ---------------------------------------------------------------
    // Formulario
    // ---------------------------------------------------------------
    private async void OnGuardarClicked(object sender, EventArgs e)
    {
        if (!ValidarFormulario(out var producto, out var cantidad))
            return;

        Cargando.IsVisible = true;
        Cargando.IsRunning = true;
        BtnGuardar.IsEnabled = false;

        var venta = new NuevaVentaRequest(
            EntryCliente.Text.Trim(),
            new List<DetalleVentaRequest> { new(producto!.Id, cantidad) });

        var (ok, error) = await _api.RegistrarVentaAsync(venta);

        Cargando.IsVisible = false;
        Cargando.IsRunning = false;
        BtnGuardar.IsEnabled = true;

        if (!ok)
        {
            await DisplayAlert("Error", error ?? "No se pudo registrar la venta.", "OK");

            // Si el token expiró, ApiService cerró la sesión: volver al login.
            if (!_auth.IsAuthenticated)
                await Shell.Current.GoToAsync("//login");
            return;
        }

        var notaUbicacion = _ubicacionEntrega is not null ? " con punto de entrega registrado" : " sin ubicación";
        await DisplayAlert("Listo", $"Venta registrada{notaUbicacion}.", "OK");

        LimpiarFormulario();
        await Shell.Current.GoToAsync("//app/productos");
    }

    // Deja la pantalla lista para la siguiente venta. Antes, la foto de
    // evidencia y la ubicación de la venta anterior se reutilizaban.
    private void LimpiarFormulario()
    {
        EntryCliente.Text = string.Empty;
        EntryCantidad.Text = string.Empty;
        PickerProducto.SelectedIndex = -1;

        _rutaFotoEvidencia = null;
        _ubicacionEntrega = null;
        Preferences.Remove("ultima_evidencia_foto");

        ImgEvidencia.Source = null;
        ImgEvidencia.IsVisible = false;
        LblEstadoFoto.Text = string.Empty;
        LblEstadoUbicacion.Text = string.Empty;
        BtnAjustes.IsVisible = false;
        LblErrorCliente.IsVisible = false;
        LblErrorCantidad.IsVisible = false;
        LblErrorProducto.IsVisible = false;
    }

    private bool ValidarFormulario(out ProductoDto? producto, out int cantidad)
    {
        producto = null;
        cantidad = 0;
        var valido = true;

        if (string.IsNullOrWhiteSpace(EntryCliente.Text))
        {
            LblErrorCliente.Text = "El nombre del cliente es obligatorio.";
            LblErrorCliente.IsVisible = true;
            valido = false;
        }
        else
        {
            LblErrorCliente.IsVisible = false;
        }

        // Antes se llamaba a DisplayAlert sin await dentro de un método
        // síncrono; ahora el error se muestra junto al campo, igual que el resto.
        if (PickerProducto.SelectedIndex < 0 || PickerProducto.SelectedIndex >= _productos.Count)
        {
            LblErrorProducto.Text = "Selecciona un producto.";
            LblErrorProducto.IsVisible = true;
            valido = false;
        }
        else
        {
            LblErrorProducto.IsVisible = false;
            producto = _productos[PickerProducto.SelectedIndex];
        }

        if (!int.TryParse(EntryCantidad.Text, out cantidad) || cantidad <= 0)
        {
            LblErrorCantidad.Text = "Ingresa una cantidad numérica mayor a 0.";
            LblErrorCantidad.IsVisible = true;
            valido = false;
        }
        else if (producto is not null && cantidad > producto.Stock)
        {
            LblErrorCantidad.Text = $"Stock insuficiente: solo hay {producto.Stock} unidades.";
            LblErrorCantidad.IsVisible = true;
            valido = false;
        }
        else
        {
            LblErrorCantidad.IsVisible = false;
        }

        return valido;
    }
}
