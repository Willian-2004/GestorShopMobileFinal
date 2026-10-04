using System.Globalization;
using CommunityToolkit.Maui.Alerts;
using GestorShopMobile.Models;
using GestorShopMobile.Services;

namespace GestorShopMobile.Views;

// Formulario de productos (solo Administrador), usado para CREAR y para
// EDITAR. Si se navega con "nuevo-producto?id=5" entra en modo edición:
// carga el producto con GET /api/productos/5 y guarda con PUT. Sin "id"
// funciona como alta con POST /api/productos.
public partial class NuevoProductoPage : ContentPage, IQueryAttributable
{
    private readonly ApiService _api;
    private readonly AuthService _auth;
    private List<CategoriaDto> _categorias = new();
    private bool _guardando;

    // Modo edición: id del producto a modificar (null = producto nuevo)
    private int? _idEdicion;
    private bool _productoCargado;
    private bool EsEdicion => _idEdicion is not null;

    public NuevoProductoPage(ApiService api, AuthService auth)
    {
        InitializeComponent();
        _api = api;
        _auth = auth;

        // "Siguiente" en el teclado pasa al campo de precio
        EntryNombre.Completed += (_, _) => EntryPrecio.Focus();
    }

    // Recibe los parámetros de navegación de Shell (?id=...)
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var valor) && int.TryParse(valor?.ToString(), out var id))
        {
            _idEdicion = id;
            Title = "Editar producto";
            LblInstrucciones.Text = "Modifica los datos y pulsa «Guardar cambios».";
            BtnGuardar.Text = "Guardar cambios";
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_auth.IsAuthenticated)
        {
            await Shell.Current.GoToAsync("//login");
            return;
        }

        // Protección por rol: la API también lo valida ([Authorize(Roles)]),
        // pero se evita mostrar un formulario que el usuario no podrá usar.
        if (!_auth.EsAdministrador)
        {
            await DisplayAlert("Sin permisos", "Solo un Administrador puede gestionar productos.", "OK");
            await Shell.Current.GoToAsync("..");
            return;
        }

        if (_categorias.Count == 0)
            await CargarCategoriasAsync();

        if (EsEdicion && !_productoCargado)
            await CargarProductoAsync();
    }

    // Modo edición: llena el formulario con los datos actuales del producto
    private async Task CargarProductoAsync()
    {
        Cargando.IsVisible = Cargando.IsRunning = true;
        BtnGuardar.IsEnabled = false;
        var (ok, producto, error) = await _api.ObtenerProductoAsync(_idEdicion!.Value);
        Cargando.IsVisible = Cargando.IsRunning = false;
        BtnGuardar.IsEnabled = true;

        if (!ok || producto is null)
        {
            await DisplayAlert("Error", error ?? "No se pudo cargar el producto.", "OK");
            await Shell.Current.GoToAsync("..");
            return;
        }

        EntryNombre.Text = producto.Nombre;
        EntryPrecio.Text = producto.Precio.ToString("0.00", CultureInfo.InvariantCulture);
        EntryStock.Text = producto.Stock.ToString();
        PickerCategoria.SelectedIndex = _categorias.FindIndex(c => c.Id == producto.CategoriaId);

        // Los TextChanged anteriores pudieron ocultar/mostrar errores: se limpian
        LblErrorNombre.IsVisible = LblErrorPrecio.IsVisible =
            LblErrorStock.IsVisible = LblErrorCategoria.IsVisible = false;
        _productoCargado = true;
    }

    // Carga las categorías y muestra el estado debajo del selector (en vez de
    // una alerta), con opción de reintentar si falla.
    private async Task CargarCategoriasAsync(int? seleccionarId = null)
    {
        LblEstadoCategorias.Text = "Cargando categorías…";
        LblEstadoCategorias.TextColor = Colors.Gray;
        LblEstadoCategorias.IsVisible = true;
        BtnReintentarCategorias.IsVisible = false;

        var (ok, categorias, error) = await _api.ObtenerCategoriasAsync();
        if (!ok || categorias is null)
        {
            LblEstadoCategorias.Text = error ?? "No se pudieron cargar las categorías.";
            LblEstadoCategorias.TextColor = Color.FromArgb("#A32D2D");
            BtnReintentarCategorias.IsVisible = true;
            return;
        }

        _categorias = categorias;
        PickerCategoria.ItemsSource = _categorias.Select(c => c.Nombre).ToList();

        if (_categorias.Count == 0)
        {
            LblEstadoCategorias.Text = "Aún no hay categorías. Crea una con «+ Nueva categoría».";
            return;
        }

        LblEstadoCategorias.IsVisible = false;

        if (seleccionarId is not null)
            PickerCategoria.SelectedIndex = _categorias.FindIndex(c => c.Id == seleccionarId);
        else if (_categorias.Count == 1)
            PickerCategoria.SelectedIndex = 0;
    }

    private async void OnReintentarCategoriasClicked(object sender, EventArgs e) => await CargarCategoriasAsync();

    private async void OnNuevaCategoriaClicked(object sender, EventArgs e)
    {
        var nombre = await DisplayPromptAsync(
            "Nueva categoría",
            "Escribe el nombre de la categoría (por ejemplo: Bebidas, Limpieza).",
            "Crear", "Cancelar",
            placeholder: "Nombre de la categoría",
            maxLength: 40);

        if (nombre is null) return;              // canceló
        nombre = nombre.Trim();
        if (nombre.Length < 2)
        {
            await DisplayAlert("Nombre inválido", "El nombre debe tener al menos 2 caracteres.", "OK");
            return;
        }

        BtnNuevaCategoria.IsEnabled = false;
        var (ok, categoria, error) = await _api.CrearCategoriaAsync(nombre);
        BtnNuevaCategoria.IsEnabled = true;

        if (!ok || categoria is null)
        {
            await DisplayAlert("No se pudo crear", error ?? "Intenta nuevamente.", "OK");
            if (!_auth.IsAuthenticated)
                await Shell.Current.GoToAsync("//login");
            return;
        }

        // Recarga la lista y deja seleccionada la categoría recién creada
        await CargarCategoriasAsync(categoria.Id);
        LblErrorCategoria.IsVisible = false;
        await Toast.Make($"Categoría «{categoria.Nombre}» creada").Show();
    }

    // Oculta el error de un campo en cuanto el usuario lo corrige
    private void OnCampoCambiado(object? sender, EventArgs e)
    {
        if (sender == EntryNombre) LblErrorNombre.IsVisible = false;
        else if (sender == EntryPrecio) LblErrorPrecio.IsVisible = false;
        else if (sender == EntryStock) LblErrorStock.IsVisible = false;
        else if (sender == PickerCategoria) LblErrorCategoria.IsVisible = false;
    }

    private async void OnGuardarClicked(object sender, EventArgs e)
    {
        if (_guardando) return;
        if (!Validar(out var nombre, out var precio, out var stock, out var categoria))
            return;

        _guardando = true;
        EstablecerCargando(true);

        var datos = new NuevoProductoRequest(nombre, precio, stock, categoria!.Id);
        var (ok, error) = EsEdicion
            ? await _api.ActualizarProductoAsync(_idEdicion!.Value, datos)
            : await _api.CrearProductoAsync(datos);

        EstablecerCargando(false);
        _guardando = false;

        if (!ok)
        {
            await DisplayAlert("No se pudo guardar", error ?? "Intenta nuevamente.", "OK");
            if (!_auth.IsAuthenticated)
                await Shell.Current.GoToAsync("//login");
            return;
        }

        // Confirmación breve y no intrusiva; al volver, la lista de
        // productos se recarga sola (OnAppearing) y ya incluye el nuevo.
        await Toast.Make(EsEdicion
            ? $"«{nombre}» se actualizó correctamente"
            : $"«{nombre}» se agregó al catálogo").Show();
        await Shell.Current.GoToAsync("..");
    }

    private async void OnCancelarClicked(object sender, EventArgs e)
    {
        // Evita perder datos por un toque accidental
        var hayDatos = !string.IsNullOrWhiteSpace(EntryNombre.Text)
                       || !string.IsNullOrWhiteSpace(EntryPrecio.Text)
                       || !string.IsNullOrWhiteSpace(EntryStock.Text);

        var mensaje = EsEdicion
            ? "Los cambios no guardados se perderán. ¿Deseas salir?"
            : "Los datos ingresados se perderán. ¿Deseas salir?";

        if (hayDatos && !await DisplayAlert(EsEdicion ? "Descartar cambios" : "Descartar producto",
                mensaje, "Salir", "Seguir editando"))
            return;

        await Shell.Current.GoToAsync("..");
    }

    private void EstablecerCargando(bool cargando)
    {
        Cargando.IsVisible = cargando;
        Cargando.IsRunning = cargando;
        BtnGuardar.IsEnabled = !cargando;
        BtnCancelar.IsEnabled = !cargando;
        BtnGuardar.Text = cargando ? "Guardando…" : (EsEdicion ? "Guardar cambios" : "Guardar producto");
    }

    private bool Validar(out string nombre, out decimal precio, out int stock, out CategoriaDto? categoria)
    {
        var valido = true;
        nombre = EntryNombre.Text?.Trim() ?? string.Empty;
        precio = 0;
        stock = 0;
        categoria = null;

        if (nombre.Length < 2)
        {
            MostrarError(LblErrorNombre, "Ingresa un nombre de al menos 2 caracteres.");
            valido = false;
        }

        // Acepta tanto "25.50" como "25,50" (según el teclado del dispositivo)
        var textoPrecio = (EntryPrecio.Text ?? string.Empty).Trim().Replace(',', '.');
        if (!decimal.TryParse(textoPrecio, NumberStyles.Number, CultureInfo.InvariantCulture, out precio) || precio <= 0)
        {
            MostrarError(LblErrorPrecio, "Ingresa un precio mayor a 0.");
            valido = false;
        }
        else if (decimal.Round(precio, 2) != precio)
        {
            MostrarError(LblErrorPrecio, "Usa máximo 2 decimales.");
            valido = false;
        }

        if (!int.TryParse(EntryStock.Text?.Trim(), out stock) || stock < 0)
        {
            MostrarError(LblErrorStock, "Ingresa un número entero (0 o más).");
            valido = false;
        }

        if (PickerCategoria.SelectedIndex < 0 || PickerCategoria.SelectedIndex >= _categorias.Count)
        {
            MostrarError(LblErrorCategoria, "Selecciona una categoría.");
            valido = false;
        }
        else
        {
            categoria = _categorias[PickerCategoria.SelectedIndex];
        }

        return valido;
    }

    private static void MostrarError(Label etiqueta, string mensaje)
    {
        etiqueta.Text = mensaje;
        etiqueta.IsVisible = true;
    }
}
