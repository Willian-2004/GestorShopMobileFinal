using GestorShopAPI.Models;

namespace GestorShopAPI.Data;

public static class SeedData
{
    // La contraseña del usuario administrador inicial se recibe desde la
    // configuración (Seed:AdminPassword) para no dejarla escrita en el código.
    public static void Inicializar(AppDbContext db, string? adminPassword, ILogger logger)
    {
        if (db.Categorias.Any()) return; // ya sembrado

        var electronica = new Categoria { Nombre = "Electrónica" };
        var hogar = new Categoria { Nombre = "Hogar" };
        db.Categorias.AddRange(electronica, hogar);
        db.SaveChanges();

        var productos = new List<Producto>();
        for (int i = 1; i <= 50; i++)
        {
            productos.Add(new Producto
            {
                Nombre = $"Producto {i}",
                Precio = 10 + i,
                Stock = 100,
                CategoriaId = i % 2 == 0 ? electronica.Id : hogar.Id
            });
        }
        db.Productos.AddRange(productos);

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning("Seed:AdminPassword no está configurado: no se creó el usuario 'admin'.");
        }
        else
        {
            db.Usuarios.Add(new Usuario
            {
                Username = "admin",
                // Hash simple con SHA256 solo para fines del taller
                PasswordHash = BCryptSimple.Hash(adminPassword),
                Rol = "Administrador"
            });
        }

        db.SaveChanges();
    }
}

// Hash simple (NO usar en producción real: para un sistema real usar BCrypt.Net-Next o Identity)
public static class BCryptSimple
{
    public static string Hash(string password)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }

    public static bool Verify(string password, string hash) => Hash(password) == hash;
}
