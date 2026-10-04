namespace GestorShopAPI.Models;

public class Producto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public int Stock { get; set; }

    public int CategoriaId { get; set; }

    // Navegación marcada como "virtual": se usa LAZY LOADING aquí.
    // Justificación (ver README): en la mayoría de los endpoints de productos
    // (listados, búsquedas, actualizaciones de stock) NO se necesita el detalle
    // de la categoría, por lo que cargarla solo cuando se accede evita trabajo
    // innecesario en el 90% de las peticiones.
    public virtual Categoria? Categoria { get; set; }
}
