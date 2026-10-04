namespace GestorShopMobile.Services;

public static class AppConfig
{
    private const string ClavePreferencia = "api_base_url";

    // Valor por defecto según la plataforma de destino (Semana 9: "URL base
    // mediante variables de entorno" + "autorizar de forma acotada el
    // tráfico hacia backend local"):
    //  - Emulador Android: 10.0.2.2 es el alias que Android usa para
    //    referirse al "localhost" de la máquina anfitriona.
    //  - Puede sobreescribirse en tiempo de compilación exportando la
    //    variable de entorno GESTORSHOP_API_BASE_URL antes de "dotnet build"
    //    (útil en CI o para apuntar a un backend desplegado).
    //  - Para un dispositivo físico en la misma red, no existe el concepto
    //    de "variable de entorno" dentro del binario ya compilado, así que
    //    se permite sobreescribir en runtime desde PerfilPage (Preferences,
    //    persistencia local) con la IP LAN del equipo que corre la API.
    private static string ValorPorDefecto =>
        Environment.GetEnvironmentVariable("GESTORSHOP_API_BASE_URL")
        ?? (DeviceInfo.DeviceType == DeviceType.Virtual
            ? "http://10.0.2.2:5164"
            : "http://192.168.1.100:5164"); // <-- reemplazar por la IP LAN real al probar en dispositivo físico

    public static string ApiBaseUrl
    {
        get => Preferences.Get(ClavePreferencia, ValorPorDefecto);
        set => Preferences.Set(ClavePreferencia, value);
    }

    // Borra la URL guardada manualmente y vuelve al valor por defecto
    // (útil si se guardó una IP incorrecta y la app ya no conecta).
    public static void Restablecer() => Preferences.Remove(ClavePreferencia);

    public static string UrlPorDefecto => ValorPorDefecto;
}
