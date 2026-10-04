using GestorShopAPI.Data;
using GestorShopAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorShopAPI.Controllers;

public record LoginRequest(string Username, string Password);

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtService _jwt;

    public AuthController(AppDbContext db, JwtService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        // Única consulta a BD del flujo de autenticación (por login, no por
        // request protegido): se busca el usuario, se valida el password y
        // se emite el token con el rol embebido en las claims.
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Username == req.Username);

        if (usuario is null || !BCryptSimple.Verify(req.Password, usuario.PasswordHash))
            return Unauthorized(new { mensaje = "Credenciales inválidas" });

        var token = _jwt.GenerarToken(usuario);
        return Ok(new { token, rol = usuario.Rol });
    }
}
