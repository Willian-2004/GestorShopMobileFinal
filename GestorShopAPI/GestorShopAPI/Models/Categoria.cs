namespace GestorShopAPI.Models;

public class Categoria
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;

    // Navegación virtual para permitir lazy loading cuando se necesite
    public virtual ICollection<Producto> Productos { get; set; } = new List<Producto>();
}
