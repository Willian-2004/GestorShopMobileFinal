using GestorShopAPI.Data;
using GestorShopAPI.Models;
using GestorShopAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace GestorShopAPI.Controllers;

public record NuevoProductoRequest(string Nombre, decimal Precio, int Stock, int CategoriaId);

[ApiController]
[Route("api/[controller]")]
public class ProductosController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICacheService _cache;

    public ProductosController(AppDbContext db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    // -----------------------------------------------------------------
    // DIAGNÓSTICO: operación con problema N+1 (se deja solo como evidencia
    // de "antes", NO usar en producción). Por cada producto, al acceder a
    // "p.Categoria!.Nombre" se dispara una consulta adicional porque la
    // navegación es lazy y aquí no se hizo Include(): 1 consulta para traer
    // los productos + N consultas (una por producto) para su categoría.
    // -----------------------------------------------------------------
    [HttpGet("sin-optimizar")]
    public async Task<IActionResult> ListarSinOptimizar()
    {
        var sw = Stopwatch.StartNew();
        var productos = await _db.Productos.ToListAsync();

        var resultado = productos.Select(p => new
        {
            p.Id,
            p.Nombre,
            p.Precio,
            Categoria = p.Categoria!.Nombre // dispara lazy load individual -> N+1
        }).ToList();

        sw.Stop();
        return Ok(new { tiempoMs = sw.ElapsedMilliseconds, total = resultado.Count, resultado });
    }

    // -----------------------------------------------------------------
    // OPTIMIZADO: corrige el N+1 con eager loading explícito (Include) +
    // proyección a solo los campos necesarios (Select), y agrega caché
    // cache-aside porque el catálogo de productos cambia poco (TTL 60s) y
    // se consulta con mucha frecuencia desde la app móvil.
    // -----------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var sw = Stopwatch.StartNew();

        var resultado = await _cache.GetOrCreateAsync(
            "productos:listado",
            TimeSpan.FromSeconds(60),
            async () => await _db.Productos
                .Include(p => p.Categoria)          // eager loading: 1 sola consulta con JOIN
                .Select(p => new
                {
                    p.Id,
                    p.Nombre,
                    p.Precio,
                    p.Stock,
                    Categoria = p.Categoria!.Nombre
                })
                .ToListAsync());

        sw.Stop();
        return Ok(new { tiempoMs = sw.ElapsedMilliseconds, total = resultado.Count, resultado });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> ObtenerPorId(int id)
    {
        // Aquí SÍ conviene lazy loading: se trae un solo producto y su
        // categoría se carga solo al acceder a p.Categoria (1 consulta extra,
        // únicamente para este registro). Se devuelve un objeto plano para no
        // serializar la colección Categoria.Productos completa.
        var producto = await _db.Productos.FindAsync(id);
        if (producto is null)
            return NotFound(new { mensaje = $"El producto {id} no existe" });

        return Ok(new
        {
            producto.Id,
            producto.Nombre,
            producto.Precio,
            producto.Stock,
            producto.CategoriaId,
            Categoria = producto.Categoria?.Nombre
        });
    }

    [Authorize(Roles = "Administrador")]
    [HttpPost]
    public async Task<IActionResult> Crear(NuevoProductoRequest req)
    {
        // Validaciones de negocio (la app móvil también valida, pero el
        // backend nunca debe confiar solo en el cliente).
        var error = await ValidarAsync(req, idExcluido: null);
        if (error is not null) return error;
        var nombre = req.Nombre.Trim();

        var producto = new Producto
        {
            Nombre = nombre,
            Precio = req.Precio,
            Stock = req.Stock,
            CategoriaId = req.CategoriaId
        };

        _db.Productos.Add(producto);
        await _db.SaveChangesAsync();

        // Invalidación explícita del caché tras una escritura
        _cache.Invalidate("productos:listado");

        return CreatedAtAction(nameof(ObtenerPorId), new { id = producto.Id },
            new { producto.Id, producto.Nombre, producto.Precio, producto.Stock, producto.CategoriaId });
    }

    // -----------------------------------------------------------------
    // ACTUALIZAR (U del CRUD): reemplaza nombre, precio, stock y categoría.
    // -----------------------------------------------------------------
    [Authorize(Roles = "Administrador")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Actualizar(int id, NuevoProductoRequest req)
    {
        var producto = await _db.Productos.FindAsync(id);
        if (producto is null)
            return NotFound(new { mensaje = $"El producto {id} no existe" });

        var error = await ValidarAsync(req, idExcluido: id);
        if (error is not null) return error;

        producto.Nombre = req.Nombre.Trim();
        producto.Precio = req.Precio;
        producto.Stock = req.Stock;
        producto.CategoriaId = req.CategoriaId;
        await _db.SaveChangesAsync();

        _cache.Invalidate("productos:listado");

        return Ok(new { producto.Id, producto.Nombre, producto.Precio, producto.Stock, producto.CategoriaId });
    }

    // -----------------------------------------------------------------
    // ELIMINAR (D del CRUD). Regla de negocio: un producto que ya aparece
    // en ventas no se elimina, para no perder la integridad del historial
    // (por defecto EF Core borraría en cascada sus líneas de venta).
    // -----------------------------------------------------------------
    [Authorize(Roles = "Administrador")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        var producto = await _db.Productos.FindAsync(id);
        if (producto is null)
            return NotFound(new { mensaje = $"El producto {id} no existe" });

        if (await _db.DetallesVenta.AnyAsync(d => d.ProductoId == id))
            return Conflict(new
            {
                mensaje = $"No se puede eliminar \"{producto.Nombre}\" porque tiene ventas registradas. Puedes dejar su stock en 0."
            });

        _db.Productos.Remove(producto);
        await _db.SaveChangesAsync();

        _cache.Invalidate("productos:listado");

        return NoContent();
    }

    [Authorize(Roles = "Administrador")]
    [HttpPut("{id}/stock")]
    public async Task<IActionResult> ActualizarStock(int id, [FromBody] int nuevoStock)
    {
        var producto = await _db.Productos.FindAsync(id);
        if (producto is null) return NotFound();

        producto.Stock = nuevoStock;
        await _db.SaveChangesAsync();
        _cache.Invalidate("productos:listado");

        return NoContent();
    }

    // Validaciones compartidas por Crear y Actualizar. Devuelve null si los
    // datos son válidos, o la respuesta de error (400 / 409) si no lo son.
    private async Task<IActionResult?> ValidarAsync(NuevoProductoRequest req, int? idExcluido)
    {
        var nombre = req.Nombre?.Trim() ?? string.Empty;
        if (nombre.Length < 2)
            return BadRequest(new { mensaje = "El nombre debe tener al menos 2 caracteres" });
        if (req.Precio <= 0)
            return BadRequest(new { mensaje = "El precio debe ser mayor a 0" });
        if (req.Stock < 0)
            return BadRequest(new { mensaje = "El stock no puede ser negativo" });
        if (!await _db.Categorias.AnyAsync(c => c.Id == req.CategoriaId))
            return BadRequest(new { mensaje = "La categoría seleccionada no existe" });

        var nombreMinusculas = nombre.ToLower();
        if (await _db.Productos.AnyAsync(p => p.Nombre.ToLower() == nombreMinusculas && p.Id != idExcluido))
            return Conflict(new { mensaje = $"Ya existe un producto llamado \"{nombre}\"" });

        return null;
    }
}
