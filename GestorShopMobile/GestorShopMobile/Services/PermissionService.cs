namespace GestorShopMobile.Services;

public enum ResultadoPermiso
{
    Concedido,
    DenegadoTemporal,      // el usuario puede volver a intentarlo
    DenegadoPermanente,    // "no volver a preguntar" -> hay que ir a Ajustes
    NoDisponible           // el servicio del dispositivo está apagado (ej. GPS)
}

public class PermissionService
{
    // Cámara: usada en RegistrarVentaPage para adjuntar evidencia fotográfica
    // del despacho. Capacidad ESENCIAL para ese flujo (sin foto no hay
    // evidencia de entrega), por lo que su ausencia bloquea solo ese paso,
    // no el resto de la app.
    public async Task<ResultadoPermiso> SolicitarCamaraAsync()
    {
        var estado = await Permissions.CheckStatusAsync<Permissions.Camera>();

        if (estado == PermissionStatus.Granted)
            return ResultadoPermiso.Concedido;

        // Se pide en el momento de uso (no al abrir la app), precedido de
        // una explicación breve mostrada por la página que llama a este
        // método antes de invocar SolicitarCamaraAsync().
        estado = await Permissions.RequestAsync<Permissions.Camera>();

        return estado switch
        {
            PermissionStatus.Granted => ResultadoPermiso.Concedido,
            PermissionStatus.Denied => ResultadoPermiso.DenegadoTemporal,
            // En Android, tras "no volver a preguntar", RequestAsync no
            // vuelve a mostrar el diálogo del sistema y devuelve Denied de
            // forma inmediata sin interacción visible; lo tratamos como
            // denegación permanente para ofrecer el atajo a Ajustes.
            _ => ResultadoPermiso.DenegadoPermanente
        };
    }

    // Ubicación: usada para registrar el punto de entrega de la venta.
    // Capacidad OPCIONAL: si no está disponible, la venta se registra igual
    // pero sin coordenadas (degradación elegante).
    public async Task<(ResultadoPermiso resultado, Location? ubicacion)> SolicitarUbicacionAsync()
    {
        var estado = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

        if (estado != PermissionStatus.Granted)
        {
            estado = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (estado != PermissionStatus.Granted)
                return (ResultadoPermiso.DenegadoPermanente, null);
        }

        // El permiso puede estar concedido y aun así el servicio de
        // ubicación del sistema (GPS) estar apagado: son dos condiciones
        // distintas, tal como pide el taller verificarlas por separado.
        try
        {
            var ubicacion = await Geolocation.GetLastKnownLocationAsync()
                             ?? await Geolocation.GetLocationAsync(
                                 new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(8)));
            return ubicacion is not null
                ? (ResultadoPermiso.Concedido, ubicacion)
                : (ResultadoPermiso.NoDisponible, null);
        }
        catch (FeatureNotEnabledException)
        {
            return (ResultadoPermiso.NoDisponible, null); // GPS apagado
        }
        catch (Exception)
        {
            return (ResultadoPermiso.NoDisponible, null);
        }
    }

    public void AbrirAjustesDeLaApp() => AppInfo.Current.ShowSettingsUI();
}
