using Microsoft.Extensions.Caching.Memory;

namespace GestorShopAPI.Services;

// Implementado con IMemoryCache para que el taller corra sin dependencias
// externas. En producción esta misma interfaz se implementaría contra Redis
// (IDistributedCache) sin tocar los controladores que la consumen.
public class CacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<CacheService> _logger;

    public CacheService(IMemoryCache cache, ILogger<CacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T> GetOrCreateAsync<T>(string clave, TimeSpan ttl, Func<Task<T>> factory)
    {
        if (_cache.TryGetValue(clave, out T? valorEnCache) && valorEnCache is not null)
        {
            _logger.LogInformation("CACHE HIT: {Clave}", clave);
            return valorEnCache;
        }

        _logger.LogInformation("CACHE MISS: {Clave} -> consultando origen", clave);
        var valor = await factory();
        _cache.Set(clave, valor, ttl);
        return valor;
    }

    public void Invalidate(string clave)
    {
        _logger.LogInformation("CACHE INVALIDATE: {Clave}", clave);
        _cache.Remove(clave);
    }
}
