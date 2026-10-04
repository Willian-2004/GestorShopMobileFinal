namespace GestorShopAPI.Services;

public interface ICacheService
{
    /// <summary>
    /// Patrón cache-aside: intenta leer de caché; si no existe, ejecuta
    /// "factory" (normalmente una consulta a la BD), guarda el resultado
    /// con un tiempo de vida (TTL) y lo devuelve.
    /// </summary>
    Task<T> GetOrCreateAsync<T>(string clave, TimeSpan ttl, Func<Task<T>> factory);

    /// <summary>Invalidación explícita: se llama tras cualquier escritura (POST/PUT/DELETE).</summary>
    void Invalidate(string clave);
}
