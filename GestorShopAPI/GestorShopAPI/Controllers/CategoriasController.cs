using GestorShopAPI.Data;
using GestorShopAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorShopAPI.Controllers;

public record NuevaCategoriaRequest(string Nombre);

// Categorías para el selector del formulario "Nuevo producto" de la app
// móvil. El Administrador también puede crear categorías nuevas.
[ApiController]
[Route("api/[controller]")]
public class CategoriasController : ControllerBase
{
    private readonly AppDbContext _db;

    public CategoriasController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var categorias = await _db.Categorias
            .OrderBy(c => c.Nombre)
            .Select(c => new { c.Id, c.Nombre })
            .ToListAsync();

        return Ok(categorias);
    }

    [Authorize(Roles = "Administrador")]
    [HttpPost]
    public async Task<IActionResult> Crear(NuevaCategoriaRequest req)
    {
        var nombre = req.Nombre?.Trim() ?? string.Empty;
        if (nombre.Length < 2)
            return BadRequest(new { mensaje = "El nombre de la categoría debe tener al menos 2 caracteres" });
        if (nombre.Length > 40)
            return BadRequest(new { mensaje = "El nombre de la categoría es demasiado largo (máx. 40)" });

        var minusculas = nombre.ToLower();
        var existente = await _db.Categorias.FirstOrDefaultAsync(c => c.Nombre.ToLower() == minusculas);
        if (existente is not null)
            return Conflict(new { mensaje = $"La categoría \"{existente.Nombre}\" ya existe" });

        var categoria = new Categoria { Nombre = nombre };
        _db.Categorias.Add(categoria);
        await _db.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, new { categoria.Id, categoria.Nombre });
    }
}
