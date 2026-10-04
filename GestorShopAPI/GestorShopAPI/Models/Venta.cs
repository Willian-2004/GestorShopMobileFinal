namespace GestorShopAPI.Models;

public class Venta
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string Cliente { get; set; } = string.Empty;
    public decimal Total { get; set; }

    // NO es "virtual": esta relación se carga siempre con EAGER LOADING
    // (Include) desde el controlador. Justificación (ver README): una venta
    // sin sus detalles no tiene sentido de negocio -- prácticamente el 100%
    // de las consultas de ventas necesitan también sus líneas de detalle,
    // así que forzar eager loading evita el patrón N+1 (1 consulta por venta
    // para traer sus detalles) desde el diseño.
    public ICollection<DetalleVenta> Detalles { get; set; } = new List<DetalleVenta>();
}

public class DetalleVenta
{
    public int Id { get; set; }
    public int VentaId { get; set; }
    public int ProductoId { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }

    public Venta? Venta { get; set; }
    public Producto? Producto { get; set; }
}
