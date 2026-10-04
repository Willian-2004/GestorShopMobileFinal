namespace GestorShopMobile.Models;

public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token, string Rol);
