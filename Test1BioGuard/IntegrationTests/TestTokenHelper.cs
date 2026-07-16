using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace Test1BioGuard.IntegrationTests;

public static class TestTokenHelper
{
    private const string JwtKey = "BioGuard2024SecretKeyForJWTAuthentication!@#$%^&*()";
    private const string Issuer = "BioGuardApi";
    private const string Audience = "BioGuardApp";

    public static string GenerateToken(string userId, string role, Dictionary<string, string>? extraClaims = null)
    {
        var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(JwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("sub", userId),
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Role, role)
        };

        if (extraClaims != null)
        {
            foreach (var kv in extraClaims)
                claims.Add(new Claim(kv.Key, kv.Value));
        }

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string GenerateDuenoToken(string userId = "user123")
        => GenerateToken(userId, "dueno");

    public static string GeneratePacienteToken(string pacienteId)
        => GenerateToken(pacienteId, "paciente", new() { { "paciente_id", pacienteId } });

    public static string GenerateCuidadorToken(string cuidadorId)
        => GenerateToken(cuidadorId, "cuidador");
}
