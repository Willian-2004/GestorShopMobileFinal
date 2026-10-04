using GestorShopAPI.Data;
using GestorShopAPI.Models;
using GestorShopAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorShopAPI.Controllers;

public record NuevaVentaRequest(string Cliente, List<DetalleVentaRequest> Detalles);
public record DetalleVentaRequest(int ProductoId, int Cantidad);

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class VentasController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IBackgroundTaskQueue _cola;
    private readonly ICacheService _cache;

    public VentasController(AppDbContext db, IBackgroundTaskQueue cola, ICacheService cache)
    {
        _db = db;
        _cola = cola;
        _cache = cache;
    }

    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        // Eager loading forzado: siempre se necesitan los detalles junto
        // con la venta (ver justificación en Venta.cs), así que se usa
        // Include() en vez de dejarlo lazy.
        // Además se proyecta a un DTO con el nombre del producto de cada
        // línea (JOIN en la misma consulta), que es lo que muestra el
        // historial de ventas de la app móvil.
        var ventas = await _db.Ventas
            .Include(v => v.Detalles)
            .OrderByDescending(v => v.Fecha)
            .Select(v => new
            {
                v.Id,
                v.Fecha,
                v.Cliente,
                v.Total,
                Detalles = v.Detalles.Select(d => new
                {
                    d.ProductoId,
                    Producto = d.Producto!.Nombre,
                    d.Cantidad,
                    d.PrecioUnitario
                }).ToList()
            })
            .ToListAsync();

        // SQLite no guarda la zona horaria: se marca la fecha como UTC para
        // que el cliente pueda convertirla a su hora local correctamente.
        return Ok(ventas.Select(v => new
        {
            v.Id,
            Fecha = DateTime.SpecifyKind(v.Fecha, DateTimeKind.Utc),
            v.Cliente,
            v.Total,
            v.Detalles
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Registrar(NuevaVentaRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Cliente))
            return BadRequest(new { mensaje = "El nombre del cliente es obligatorio" });
        if (req.Detalles is null || req.Detalles.Count == 0)
            return BadRequest(new { mensaje = "La venta debe tener al menos un producto" });

        var venta = new Venta { Cliente = req.Cliente.Trim() };

        foreach (var linea in req.Detalles)
        {
            if (linea.Cantidad <= 0)
                return BadRequest(new { mensaje = "La cantidad debe ser mayor a 0" });

            var producto = await _db.Productos.FindAsync(linea.ProductoId);
            if (producto is null)
                return BadRequest(new { mensaje = $"Producto {linea.ProductoId} no existe" });

            if (producto.Stock < linea.Cantidad)
                return BadRequest(new { mensaje = $"Stock insuficiente para {producto.Nombre} (disponible: {producto.Stock})" });

            // Descuenta el stock vendido (antes la venta no afectaba el inventario)
            producto.Stock -= linea.Cantidad;

            venta.Detalles.Add(new DetalleVenta
            {
                ProductoId = producto.Id,
                Cantidad = linea.Cantidad,
                PrecioUnitario = producto.Precio
            });
            venta.Total += producto.Precio * linea.Cantidad;
        }

        _db.Ventas.Add(venta);
        await _db.SaveChangesAsync();

        // El stock cambió: se invalida el listado en caché para que la app
        // móvil vea el inventario actualizado.
        _cache.Invalidate("productos:listado");

        // TAREA ASÍNCRONA: enviar comprobante / actualizar reportes NO debe
        // bloquear la respuesta HTTP al cliente móvil. Se encola y la
        // procesa QueuedHostedService en segundo plano.
        var ventaId = venta.Id;
        _cola.Encolar(async (sp, ct) =>
        {
            var scopedDb = sp.GetRequiredService<AppDbContext>();
            await Task.Delay(2000, ct); // simula envío de correo / generación de PDF
            Console.WriteLine($"[Worker] Comprobante de venta #{ventaId} procesado en segundo plano.");
        });

        // Se devuelve un objeto plano (sin navegaciones) para no serializar
        // referencias circulares Venta <-> DetalleVenta.
        return StatusCode(StatusCodes.Status201Created, new
        {
            venta.Id,
            venta.Fecha,
            venta.Cliente,
            venta.Total,
            Detalles = venta.Detalles.Select(d => new { d.ProductoId, d.Cantidad, d.PrecioUnitario })
        });
    }
}
