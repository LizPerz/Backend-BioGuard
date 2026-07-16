using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.DTOs;
using BioGuard.Api.Models;

namespace BioGuard.Api.Services;

public class AuthService
{
    private readonly IMongoDbContext _db;
    private readonly string _jwtKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expirationMinutes;

    public AuthService(IMongoDbContext db, IConfiguration config)
    {
        _db = db;
        _jwtKey = config["Jwt:Key"] is { Length: > 0 } k ? k
            : Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
            ?? throw new InvalidOperationException("JWT secret key not configured.");
        _issuer = config["Jwt:Issuer"] ?? "BioGuardApi";
        _audience = config["Jwt:Audience"] ?? "BioGuardApp";
        _expirationMinutes = int.Parse(config["Jwt:ExpirationMinutes"] ?? "1440");
    }

    // ── Register ───────────────────────────────────────────

    public async Task<AuthResponse?> RegisterWebAsync(RegisterWebRequest request)
    {
        var exists = await _db.FindFirstOrDefaultAsync(_db.UsuariosWeb, u => u.Correo == request.Correo);
        if (exists != null) return null;

        var plan = await _db.FindFirstOrDefaultAsync(_db.Planes, p => p.Nombre == request.PlanNombre);
        if (plan == null) return null;

        var user = new UsuarioWeb
        {
            Nombre = request.Nombre,
            ApellidoPaterno = request.ApellidoPaterno,
            ApellidoMaterno = request.ApellidoMaterno,
            Correo = request.Correo,
            PasswordHash = BCryptHelper.HashPassword(request.Password),
            ProveedorAuth = "local",
            PlanId = plan.Id,
            Activo = true,
            FechaRegistro = DateTime.UtcNow
        };

        await _db.UsuariosWeb.InsertOneAsync(user);
        var token = GenerateToken(user.Id, user.Correo, "dueno");

        return new AuthResponse(token, user.Id, $"{user.Nombre} {user.ApellidoPaterno}", "dueno", plan.Nombre);
    }

    // ── Login Web ──────────────────────────────────────────

    public async Task<AuthResponse?> LoginWebAsync(LoginWebRequest request)
    {
        var user = await _db.FindFirstOrDefaultAsync(_db.UsuariosWeb, u => u.Correo == request.Correo);
        if (user == null || !user.Activo) return null;

        if (!BCryptHelper.VerifyPassword(request.Password, user.PasswordHash)) return null;

        var plan = await _db.FindFirstOrDefaultAsync(_db.Planes, p => p.Id == user.PlanId);
        var token = GenerateToken(user.Id, user.Correo, "dueno");

        return new AuthResponse(token, user.Id, $"{user.Nombre} {user.ApellidoPaterno}", "dueno", plan?.Nombre ?? "Sin plan");
    }

    // ── Login Google ───────────────────────────────────────

    public async Task<AuthResponse?> LoginGoogleAsync(LoginGoogleRequest request)
    {
        string? email = await ValidarTokenGoogleAsync(request.IdToken);
        if (email == null) return null;

        var user = await _db.FindFirstOrDefaultAsync(_db.UsuariosWeb, u => u.Correo == email);

        if (user == null)
        {
            var plan = await _db.FindFirstOrDefaultAsync(_db.Planes, p => p.Nombre == "Gratis");
            if (plan == null) return null;

            user = new UsuarioWeb
            {
                Nombre = email.Split('@')[0],
                ApellidoPaterno = "",
                ApellidoMaterno = "",
                Correo = email,
                PasswordHash = "",
                ProveedorAuth = "google",
                GoogleId = request.IdToken,
                PlanId = plan.Id,
                Activo = true,
                FechaRegistro = DateTime.UtcNow
            };

            await _db.UsuariosWeb.InsertOneAsync(user);
        }

        var userPlan = await _db.FindFirstOrDefaultAsync(_db.Planes, p => p.Id == user.PlanId);
        var token = GenerateToken(user.Id, user.Correo, "dueno");

        return new AuthResponse(token, user.Id, $"{user.Nombre} {user.ApellidoPaterno}", "dueno", userPlan?.Nombre ?? "Sin plan");
    }

    // ── Login por Código (Móvil) ───────────────────────────

    public async Task<AuthResponse?> LoginByCodigoAsync(LoginCodigoRequest request)
    {
        var paciente = await _db.FindFirstOrDefaultAsync(_db.Pacientes, p => p.CodigoAccesoQr == request.CodigoAcceso);
        if (paciente != null)
        {
            var token = GenerateToken(paciente.Id, paciente.CodigoAccesoQr, "paciente");
            return new AuthResponse(token, paciente.Id, paciente.Nombre, "paciente", "paciente");
        }

        var cuidador = await _db.FindFirstOrDefaultAsync(_db.Cuidadores, c => c.CodigoAccesoQr == request.CodigoAcceso);
        if (cuidador != null)
        {
            var token = GenerateToken(cuidador.Id, cuidador.CodigoAccesoQr, "cuidador");
            return new AuthResponse(token, cuidador.Id, cuidador.Nombre, "cuidador", "cuidador");
        }

        return null;
    }

    // ── 2FA ────────────────────────────────────────────────

    public async Task<bool> Enviar2FAAsync(Enviar2FARequest request)
    {
        var user = await _db.FindFirstOrDefaultAsync(_db.UsuariosWeb, u => u.Correo == request.Correo);
        if (user == null || !user.Activo) return false;

        var codigo = RandomNumberString(6);
        var expira = DateTime.UtcNow.AddMinutes(10);

        var update = Builders<UsuarioWeb>.Update
            .Set(u => u.TwoFactorCode, codigo)
            .Set(u => u.TwoFactorExpira, expira)
            .Set(u => u.TwoFactorVerificado, false);

        await _db.UsuariosWeb.UpdateOneAsync(u => u.Id == user.Id, update);

        return true;
    }

    public async Task<AuthResponse?> Verificar2FAAsync(Verificar2FARequest request)
    {
        var user = await _db.FindFirstOrDefaultAsync(_db.UsuariosWeb, u => u.Correo == request.Correo);
        if (user == null || !user.Activo) return null;

        if (string.IsNullOrEmpty(user.TwoFactorCode)) return null;
        if (user.TwoFactorExpira == null || user.TwoFactorExpira < DateTime.UtcNow) return null;
        if (user.TwoFactorCode != request.Codigo) return null;

        var update = Builders<UsuarioWeb>.Update
            .Set(u => u.TwoFactorCode, null)
            .Set(u => u.TwoFactorExpira, null)
            .Set(u => u.TwoFactorVerificado, true);

        await _db.UsuariosWeb.UpdateOneAsync(u => u.Id == user.Id, update);

        var plan = await _db.FindFirstOrDefaultAsync(_db.Planes, p => p.Id == user.PlanId);
        var token = GenerateToken(user.Id, user.Correo, "dueno");

        return new AuthResponse(token, user.Id, $"{user.Nombre} {user.ApellidoPaterno}", "dueno", plan?.Nombre ?? "Sin plan");
    }

    // ── Forgot Password ────────────────────────────────────

    public async Task<bool> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var user = await _db.FindFirstOrDefaultAsync(_db.UsuariosWeb, u => u.Correo == request.Correo);
        if (user == null || !user.Activo) return false;

        var token = GenerateRandomToken();
        var expira = DateTime.UtcNow.AddHours(1);

        var update = Builders<UsuarioWeb>.Update
            .Set(u => u.ResetPasswordToken, token)
            .Set(u => u.ResetPasswordExpira, expira);

        await _db.UsuariosWeb.UpdateOneAsync(u => u.Id == user.Id, update);

        return true;
    }

    public async Task<bool> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var user = await _db.FindFirstOrDefaultAsync(_db.UsuariosWeb, u => u.ResetPasswordToken == request.Token);

        if (user == null) return false;
        if (user.ResetPasswordExpira == null || user.ResetPasswordExpira < DateTime.UtcNow) return false;

        var update = Builders<UsuarioWeb>.Update
            .Set(u => u.PasswordHash, BCryptHelper.HashPassword(request.NuevaPassword))
            .Set(u => u.ResetPasswordToken, null)
            .Set(u => u.ResetPasswordExpira, null);

        await _db.UsuariosWeb.UpdateOneAsync(u => u.Id == user.Id, update);

        return true;
    }

    // ── Cambiar Password (logueado) ────────────────────────

    public async Task<bool> CambiarPasswordAsync(string userId, CambiarPasswordRequest request)
    {
        var user = await _db.FindFirstOrDefaultAsync(_db.UsuariosWeb, u => u.Id == userId);
        if (user == null) return false;

        if (!BCryptHelper.VerifyPassword(request.PasswordActual, user.PasswordHash)) return false;

        var update = Builders<UsuarioWeb>.Update
            .Set(u => u.PasswordHash, BCryptHelper.HashPassword(request.NuevaPassword));

        await _db.UsuariosWeb.UpdateOneAsync(u => u.Id == userId, update);

        return true;
    }

    // ── Helpers ────────────────────────────────────────────

    private string GenerateToken(string id, string email, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, id),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_expirationMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string RandomNumberString(int length)
    {
        var numbers = new char[length];
        for (int i = 0; i < length; i++)
            numbers[i] = (char)Random.Shared.Next('0', '9');
        return new string(numbers);
    }

    private static string GenerateRandomToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_");
    }

    private static async Task<string?> ValidarTokenGoogleAsync(string idToken)
    {
        await Task.CompletedTask;
        return null;
    }
}

public static class BCryptHelper
{
    public static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, 12);
    }

    public static bool VerifyPassword(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
