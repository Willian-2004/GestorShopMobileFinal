namespace GestorShopMobile.Models;

public class ProductoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public int Stock { get; set; }
    public string Categoria { get; set; } = string.Empty;
}

public class ProductosResponse
{
    public long TiempoMs { get; set; }
    public int Total { get; set; }
    public List<ProductoDto> Resultado { get; set; } = new();
}

public record DetalleVentaRequest(int ProductoId, int Cantidad);
public record NuevaVentaRequest(string Cliente, List<DetalleVentaRequest> Detalles);

// ---- Historial de ventas ----
public class DetalleVentaDto
{
    public int ProductoId { get; set; }
    public string Producto { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }

    public decimal Subtotal => Cantidad * PrecioUnitario;
}

public class VentaDto
{
    public int Id { get; set; }
    public DateTimeOffset Fecha { get; set; }
    public string Cliente { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public List<DetalleVentaDto> Detalles { get; set; } = new();

    // Propiedades de presentación (solo lectura) para la vista
    public string FechaLocal => Fecha.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    public string Numero => $"Venta #{Id}";
    public string ResumenProductos => string.Join("\n",
        Detalles.Select(d => $"{d.Cantidad} × {d.Producto}  —  ${d.Subtotal:0.00}"));
    public int TotalUnidades => Detalles.Sum(d => d.Cantidad);
}

// ---- Nuevo producto ----
public class CategoriaDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
}

public record NuevoProductoRequest(string Nombre, decimal Precio, int Stock, int CategoriaId);

public record NuevaCategoriaRequest(string Nombre);

// ---- Detalle de un producto (para editar) ----
public class ProductoDetalleDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public int Stock { get; set; }
    public int CategoriaId { get; set; }
    public string? Categoria { get; set; }
}
