using GestorShopMobile.Services;

namespace GestorShopMobile.Views;

// Historial de ventas: consume GET /api/ventas (protegido con JWT) y
// muestra las ventas más recientes primero, con un resumen arriba.
public partial class HistorialVentasPage : ContentPage
{
    private readonly ApiService _api;
    private readonly AuthService _auth;
    private bool _cargando;

    public HistorialVentasPage(ApiService api, AuthService auth)
    {
        InitializeComponent();
        _api = api;
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

        // Se recarga cada vez que se abre la pestaña para que una venta
        // recién registrada aparezca sin tener que refrescar a mano.
        await CargarVentasAsync();
    }

    private async void OnRefrescando(object? sender, EventArgs e) => await CargarVentasAsync();

    private async Task CargarVentasAsync()
    {
        if (_cargando) return;
        _cargando = true;

        try
        {
            Refrescar.IsRefreshing = true;
            var (ok, ventas, error) = await _api.ObtenerVentasAsync();

            if (!ok || ventas is null)
            {
                Refrescar.IsRefreshing = false;
                await DisplayAlert("Error", error ?? "No se pudo cargar el historial.", "OK");
                if (!_auth.IsAuthenticated)
                    await Shell.Current.GoToAsync("//login");
                return;
            }

            ListaVentas.ItemsSource = ventas;
            LblCantidadVentas.Text = ventas.Count.ToString();
            LblTotalVendido.Text = $"${ventas.Sum(v => v.Total):0.00}";
        }
        finally
        {
            Refrescar.IsRefreshing = false;
            _cargando = false;
        }
    }
}
