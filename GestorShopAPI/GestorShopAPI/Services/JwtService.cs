using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GestorShopAPI.Models;
using Microsoft.IdentityModel.Tokens;

namespace GestorShopAPI.Services;

public class JwtService
{
    private readonly IConfiguration _config;

    public JwtService(IConfiguration config)
    {
        _config = config;
    }

    // El Rol se guarda como CLAIM dentro del token. Así, el middleware de
    // autenticación valida la firma del token y lee el rol directamente de
    // sus claims: NO necesita ir a la base de datos en cada petición para
    // saber si el usuario es Administrador o Vendedor. Esto es lo que evita
    // las consultas redundantes en la autenticación (criterio de evaluación).
    public string GenerarToken(Usuario usuario)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Name, usuario.Username),
            new Claim(ClaimTypes.Role, usuario.Rol)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15), // access token corto
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
