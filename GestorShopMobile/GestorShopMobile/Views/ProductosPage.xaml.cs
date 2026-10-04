using CommunityToolkit.Maui.Alerts;
using GestorShopMobile.Models;
using GestorShopMobile.Services;

namespace GestorShopMobile.Views;

public partial class ProductosPage : ContentPage
{
    private readonly ApiService _api;
    private readonly AuthService _auth;
    private bool _cargando;
    private List<ProductoDto> _productos = new();

    public ProductosPage(ApiService api, AuthService auth)
    {
        InitializeComponent();
        _api = api;
        _auth = auth;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Protección de vista: si no hay sesión activa, no se muestra el
        // contenido y se redirige al login.
        if (!_auth.IsAuthenticated)
        {
            await Shell.Current.GoToAsync("//login");
            return;
        }

        ConfigurarBotonAgregar();
        LblAyudaAdmin.IsVisible = _auth.EsAdministrador;
        await CargarProductosAsync();
    }

    // El botón "+ Agregar" de la barra superior solo aparece para el rol
    // Administrador (el único que la API autoriza a crear productos).
    private void ConfigurarBotonAgregar()
    {
        ToolbarItems.Clear();
        if (_auth.EsAdministrador)
        {
            ToolbarItems.Add(new ToolbarItem
            {
                Text = "+ Agregar",
                Order = ToolbarItemOrder.Primary,
                Command = new Command(async () => await Shell.Current.GoToAsync("nuevo-producto"))
            });
        }
    }

    // Se dispara al deslizar hacia abajo (pull-to-refresh).
    private async void OnRefrescando(object? sender, EventArgs e) => await CargarProductosAsync();

    private async Task CargarProductosAsync()
    {
        if (_cargando) return; // evita peticiones duplicadas
        _cargando = true;

        try
        {
            Refrescar.IsRefreshing = true;
            var (ok, datos, error) = await _api.ObtenerProductosAsync();
            Refrescar.IsRefreshing = false;

            if (!ok || datos is null)
            {
                await DisplayAlert("Error", error ?? "No se pudo cargar el catálogo.", "OK");

                // Si el token expiró, ApiService cerró la sesión: volver al login.
                if (!_auth.IsAuthenticated)
                    await Shell.Current.GoToAsync("//login");
                return;
            }

            _productos = datos.Resultado;
            AplicarFiltro();
        }
        finally
        {
            Refrescar.IsRefreshing = false;
            _cargando = false;
        }
    }

    // ---------------------------------------------------------------
    // Búsqueda local: filtra la lista ya descargada (no hace otra
    // petición al servidor por cada letra que se escribe).
    // ---------------------------------------------------------------
    private void OnBuscarCambiado(object? sender, TextChangedEventArgs e) => AplicarFiltro();

    private void AplicarFiltro()
    {
        var texto = BuscadorProductos.Text?.Trim() ?? string.Empty;
        ListaProductos.ItemsSource = string.IsNullOrEmpty(texto)
            ? _productos
            : _productos.Where(p =>
                    p.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                    p.Categoria.Contains(texto, StringComparison.OrdinalIgnoreCase))
                .ToList();
    }

    // ---------------------------------------------------------------
    // Editar / eliminar (solo Administrador)
    // ---------------------------------------------------------------
    private async void OnProductoSeleccionado(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ProductoDto producto)
            return;

        // Se limpia la selección para poder volver a tocar el mismo producto
        ListaProductos.SelectedItem = null;

        if (!_auth.EsAdministrador)
            return;

        var accion = await DisplayActionSheet(producto.Nombre, "Cancelar", null, "Editar", "Eliminar");

        if (accion == "Editar")
            await Shell.Current.GoToAsync($"nuevo-producto?id={producto.Id}");
        else if (accion == "Eliminar")
            await EliminarProductoAsync(producto);
    }

    private async Task EliminarProductoAsync(ProductoDto producto)
    {
        // Confirmación explícita: es una acción que no se puede deshacer
        var confirmar = await DisplayAlert(
            "Eliminar producto",
            $"¿Seguro que deseas eliminar «{producto.Nombre}»? Esta acción no se puede deshacer.",
            "Eliminar", "Cancelar");
        if (!confirmar) return;

        var (ok, error) = await _api.EliminarProductoAsync(producto.Id);
        if (!ok)
        {
            await DisplayAlert("No se pudo eliminar", error ?? "Intenta nuevamente.", "OK");
            if (!_auth.IsAuthenticated)
                await Shell.Current.GoToAsync("//login");
            return;
        }

        await Toast.Make($"«{producto.Nombre}» se eliminó del catálogo").Show();
        await CargarProductosAsync();
    }
}
