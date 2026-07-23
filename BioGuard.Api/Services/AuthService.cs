using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
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
    private readonly int _refreshTokenDays;
    private readonly string? _googleClientId;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IMongoDbContext db, IConfiguration config, HttpClient httpClient, ILogger<AuthService> logger)
    {
        _db = db;
        _httpClient = httpClient;
        _logger = logger;
        _jwtKey = config["Jwt:Key"] is { Length: > 0 } k ? k
            : Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
            ?? throw new InvalidOperationException("JWT secret key not configured.");
        _issuer = config["Jwt:Issuer"] ?? "BioGuardApi";
        _audience = config["Jwt:Audience"] ?? "BioGuardApp";
        _expirationMinutes = int.Parse(config["Jwt:ExpirationMinutes"] ?? "60");
        _refreshTokenDays = int.Parse(config["Jwt:RefreshTokenDays"] ?? "7");
        _googleClientId = config["Google:ClientId"]
            ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
    }

    // ── Register ───────────────────────────────────────────

    public async Task<AuthResponse?> RegisterWebAsync(RegisterWebRequest request)
    {
        var exists = await _db.FindFirstOrDefaultAsync(_db.UsuariosWeb, u => u.Correo == request.Correo);
        if (exists != null)
        {
            _logger.LogWarning("Registration attempt with existing email: {Email}", request.Correo);
            return null;
        }

        var plan = await _db.FindFirstOrDefaultAsync(_db.Planes, p => p.Nombre == request.PlanNombre);
        if (plan == null)
        {
            _logger.LogWarning("Registration attempt with invalid plan: {PlanNombre}", request.PlanNombre);
            return null;
        }

        var (passwordValid, passwordError) = PasswordHasher.ValidateComplexity(request.Password);
        if (!passwordValid)
        {
            _logger.LogWarning("Registration with weak password for email: {Correo}", request.Correo);
            return null;
        }

        var user = new UsuarioWeb
        {
            Nombre = request.Nombre,
            ApellidoPaterno = request.ApellidoPaterno,
            ApellidoMaterno = request.ApellidoMaterno,
            Correo = request.Correo,
            PasswordHash = PasswordHasher.Hash(request.Password),
            ProveedorAuth = "local",
            PlanId = plan.Id,
            Activo = true,
            FechaRegistro = DateTime.UtcNow
        };

        await _db.UsuariosWeb.InsertOneAsync(user);
        var token = GenerateToken(user.Id, user.Correo, "dueno");
        _logger.LogInformation("User registered successfully: {UserId}", user.Id);

        return new AuthResponse(token, user.Id, $"{user.Nombre} {user.ApellidoPaterno}", "dueno", plan.Nombre);
    }

    // ── Login Web ──────────────────────────────────────────

    public async Task<AuthResponse?> LoginWebAsync(LoginWebRequest request)
    {
        var user = await _db.FindFirstOrDefaultAsync(_db.UsuariosWeb, u => u.Correo == request.Correo);
        if (user == null || !user.Activo)
        {
            _logger.LogWarning("Login attempt for inactive or non-existent user: {Email}", request.Correo);
            return null;
        }

        if (user.LockedUntil != null && user.LockedUntil > DateTime.UtcNow)
        {
            _logger.LogWarning("Login blocked - account locked until {LockedUntil}", user.LockedUntil);
            return null;
        }

        if (!PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            var attempts = user.FailedLoginAttempts + 1;
            var update = Builders<UsuarioWeb>.Update.Set(u => u.FailedLoginAttempts, attempts);
            if (attempts >= 5)
            {
                update = Builders<UsuarioWeb>.Update
                    .Set(u => u.FailedLoginAttempts, attempts)
                    .Set(u => u.LockedUntil, DateTime.UtcNow.AddMinutes(15));
                _logger.LogWarning("Account locked for user {Correo} after {Attempts} failed attempts", request.Correo, attempts);
            }
            await _db.UsuariosWeb.UpdateOneAsync(u => u.Id == user.Id, update);
            _logger.LogWarning("Invalid password for user: {UserId}", user.Id);
            return null;
        }

        if (user.FailedLoginAttempts > 0 || user.LockedUntil != null)
        {
            await _db.UsuariosWeb.UpdateOneAsync(u => u.Id == user.Id,
                Builders<UsuarioWeb>.Update
                    .Set(u => u.FailedLoginAttempts, 0)
                    .Set(u => u.LockedUntil, null));
        }

        if (user.TwoFactorHabilitado)
        {
            var codigo = RandomNumberString(6);
            var expira = DateTime.UtcNow.AddMinutes(10);
            var update2fa = Builders<UsuarioWeb>.Update
                .Set(u => u.TwoFactorCode, codigo)
                .Set(u => u.TwoFactorExpira, expira)
                .Set(u => u.TwoFactorVerificado, false);
            await _db.UsuariosWeb.UpdateOneAsync(u => u.Id == user.Id, update2fa);
            _logger.LogInformation("2FA required for user: {UserId}", user.Id);
            return new AuthResponse("", user.Id, "", "", "", Requires2FA: true);
        }

        var plan = await _db.FindFirstOrDefaultAsync(_db.Planes, p => p.Id == user.PlanId);
        var token = GenerateToken(user.Id, user.Correo, "dueno");
        _logger.LogInformation("User logged in successfully: {UserId}", user.Id);

        return new AuthResponse(token, user.Id, $"{user.Nombre} {user.ApellidoPaterno}", "dueno", plan?.Nombre ?? "Sin plan");
    }

    // ── Login Google ───────────────────────────────────────

    public async Task<AuthResponse?> LoginGoogleAsync(LoginGoogleRequest request)
    {
        var (email, sub) = await ValidarTokenGoogleAsync(request.IdToken);
        if (email == null || sub == null)
        {
            _logger.LogWarning("Google login attempt with invalid token");
            return null;
        }

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
                GoogleId = sub,
                PlanId = plan.Id,
                Activo = true,
                FechaRegistro = DateTime.UtcNow
            };

        await _db.UsuariosWeb.InsertOneAsync(user);
        }

        var userPlan = await _db.FindFirstOrDefaultAsync(_db.Planes, p => p.Id == user.PlanId);
        var token = GenerateToken(user.Id, user.Correo, "dueno");
        _logger.LogInformation("Google login successful for user: {UserId}", user.Id);

        return new AuthResponse(token, user.Id, $"{user.Nombre} {user.ApellidoPaterno}", "dueno", userPlan?.Nombre ?? "Sin plan");
    }

    // ── Login por Código (Móvil) ───────────────────────────

    public async Task<AuthResponse?> LoginByCodigoAsync(LoginCodigoRequest request)
    {
        var paciente = await _db.FindFirstOrDefaultAsync(_db.Pacientes, p => p.CodigoAccesoQr == request.CodigoAcceso);
        if (paciente != null)
        {
            var token = GenerateToken(paciente.Id, paciente.CodigoAccesoQr, "paciente");
            _logger.LogInformation("Patient login by code: {PacienteId}", paciente.Id);
            return new AuthResponse(token, paciente.Id, paciente.Nombre, "paciente", "paciente");
        }

        var cuidador = await _db.FindFirstOrDefaultAsync(_db.Cuidadores, c => c.CodigoAccesoQr == request.CodigoAcceso);
        if (cuidador != null)
        {
            var token = GenerateToken(cuidador.Id, cuidador.CodigoAccesoQr, "cuidador");
            _logger.LogInformation("Caregiver login by code: {CuidadorId}", cuidador.Id);
            return new AuthResponse(token, cuidador.Id, cuidador.Nombre, "cuidador", "cuidador");
        }

        _logger.LogWarning("Login by code failed: code not found");
        return null;
    }

    // ── Refresh Token ──────────────────────────────────────

    public async Task<RefreshTokenResponse?> RefreshTokenAsync(RefreshTokenRequest request, string? ip = null)
    {
        var stored = await _db.FindFirstOrDefaultAsync(_db.RefreshTokens, t =>
            CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(t.Token),
                Encoding.UTF8.GetBytes(request.RefreshToken)));
        if (stored == null || !stored.IsActive)
        {
            _logger.LogWarning("Refresh token attempt with invalid or inactive token");
            return null;
        }

        var user = await _db.FindFirstOrDefaultAsync(_db.UsuariosWeb, u => u.Id == stored.UsuarioId);
        if (user == null)
        {
            _logger.LogWarning("Refresh token user not found: {UsuarioId}", stored.UsuarioId);
            return null;
        }

        var newRefreshToken = GenerateRefreshToken();
        var oldRefreshCopy = new RefreshToken
        {
            Id = stored.Id,
            UsuarioId = stored.UsuarioId,
            Token = stored.Token,
            ExpiresAt = stored.ExpiresAt,
            CreatedAt = stored.CreatedAt,
            Ip = stored.Ip,
            ReplacedBy = newRefreshToken
        };

        await RevokeRefreshTokenAsync(oldRefreshCopy);

        await _db.RefreshTokens.InsertOneAsync(new RefreshToken
        {
            UsuarioId = user.Id,
            Token = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(_refreshTokenDays),
            Ip = ip
        });

        var accessToken = GenerateToken(user.Id, user.Correo, "dueno");
        _logger.LogInformation("Token refreshed for user: {UserId}", user.Id);

        return new RefreshTokenResponse(accessToken, newRefreshToken);
    }

    public async Task RevokeRefreshTokenAsync(RefreshToken token)
    {
        var filter = Builders<RefreshToken>.Filter.Where(t =>
            CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(t.Token),
                Encoding.UTF8.GetBytes(token.Token)) ||
            (token.ReplacedBy != null && CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(t.Token),
                Encoding.UTF8.GetBytes(token.ReplacedBy))));

        var update = Builders<RefreshToken>.Update.Set(t => t.RevokedAt, DateTime.UtcNow);

        await _db.RefreshTokens.UpdateManyAsync(filter, update);
    }

    // ── 2FA ────────────────────────────────────────────────

    public async Task<bool> Enviar2FAAsync(Enviar2FARequest request)
    {
        var user = await _db.FindFirstOrDefaultAsync(_db.UsuariosWeb, u => u.Correo == request.Correo);
        if (user == null || !user.Activo)
        {
            _logger.LogWarning("2FA send attempt for inactive or non-existent user: {Email}", request.Correo);
            return false;
        }

        var codigo = RandomNumberString(6);
        var expira = DateTime.UtcNow.AddMinutes(10);

        var update = Builders<UsuarioWeb>.Update
            .Set(u => u.TwoFactorCode, codigo)
            .Set(u => u.TwoFactorExpira, expira)
            .Set(u => u.TwoFactorVerificado, false);

        await _db.UsuariosWeb.UpdateOneAsync(u => u.Id == user.Id, update);
        _logger.LogInformation("2FA code sent to user: {UserId}", user.Id);

        return true;
    }

    public async Task<AuthResponse?> Verificar2FAAsync(Verificar2FARequest request)
    {
        var user = await _db.FindFirstOrDefaultAsync(_db.UsuariosWeb, u => u.Correo == request.Correo);
        if (user == null || !user.Activo)
        {
            _logger.LogWarning("2FA verification attempt for inactive or non-existent user: {Email}", request.Correo);
            return null;
        }

        if (string.IsNullOrEmpty(user.TwoFactorCode)) return null;
        if (user.TwoFactorExpira == null || user.TwoFactorExpira < DateTime.UtcNow)
        {
            _logger.LogWarning("2FA verification attempt with expired code for user: {UserId}", user.Id);
            return null;
        }

        var codeMatch = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(user.TwoFactorCode),
            Encoding.UTF8.GetBytes(request.Codigo));
        if (!codeMatch)
        {
            _logger.LogWarning("2FA verification failed with invalid code for user: {UserId}", user.Id);
            return null;
        }

        var update = Builders<UsuarioWeb>.Update
            .Set(u => u.TwoFactorCode, null)
            .Set(u => u.TwoFactorExpira, null)
            .Set(u => u.TwoFactorVerificado, true);

        await _db.UsuariosWeb.UpdateOneAsync(u => u.Id == user.Id, update);

        var plan = await _db.FindFirstOrDefaultAsync(_db.Planes, p => p.Id == user.PlanId);
        var token = GenerateToken(user.Id, user.Correo, "dueno");
        _logger.LogInformation("2FA verified successfully for user: {UserId}", user.Id);

        return new AuthResponse(token, user.Id, $"{user.Nombre} {user.ApellidoPaterno}", "dueno", plan?.Nombre ?? "Sin plan");
    }

    // ── Forgot Password ────────────────────────────────────

    public async Task<bool> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var user = await _db.FindFirstOrDefaultAsync(_db.UsuariosWeb, u => u.Correo == request.Correo);
        if (user == null || !user.Activo)
        {
            _logger.LogWarning("Password reset attempt for inactive or non-existent user: {Email}", request.Correo);
            return false;
        }

        var token = GenerateRandomToken();
        var expira = DateTime.UtcNow.AddHours(1);

        var update = Builders<UsuarioWeb>.Update
            .Set(u => u.ResetPasswordToken, token)
            .Set(u => u.ResetPasswordExpira, expira);

        await _db.UsuariosWeb.UpdateOneAsync(u => u.Id == user.Id, update);
        _logger.LogInformation("Password reset token generated for user: {UserId}", user.Id);

        return true;
    }

    public async Task<bool> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var user = await _db.FindFirstOrDefaultAsync(_db.UsuariosWeb, u => u.ResetPasswordToken == request.Token);

        if (user == null)
        {
            _logger.LogWarning("Password reset attempt with invalid token");
            return false;
        }
        if (user.ResetPasswordExpira == null || user.ResetPasswordExpira < DateTime.UtcNow)
        {
            _logger.LogWarning("Password reset attempt with expired token for user: {UserId}", user.Id);
            return false;
        }

        var (passwordValid, _) = PasswordHasher.ValidateComplexity(request.NuevaPassword);
        if (!passwordValid)
        {
            _logger.LogWarning("Password reset with weak password for user: {UserId}", user.Id);
            return false;
        }

        var update = Builders<UsuarioWeb>.Update
            .Set(u => u.PasswordHash, PasswordHasher.Hash(request.NuevaPassword))
            .Set(u => u.ResetPasswordToken, null)
            .Set(u => u.ResetPasswordExpira, null);

        await _db.UsuariosWeb.UpdateOneAsync(u => u.Id == user.Id, update);
        _logger.LogInformation("Password reset successfully for user: {UserId}", user.Id);

        return true;
    }

    // ── Cambiar Password (logueado) ────────────────────────

    public async Task<bool> CambiarPasswordAsync(string userId, CambiarPasswordRequest request)
    {
        var user = await _db.FindFirstOrDefaultAsync(_db.UsuariosWeb, u => u.Id == userId);
        if (user == null)
        {
            _logger.LogWarning("Password change attempt for non-existent user: {UserId}", userId);
            return false;
        }

        if (!PasswordHasher.Verify(request.PasswordActual, user.PasswordHash))
        {
            _logger.LogWarning("Password change failed: invalid current password for user: {UserId}", userId);
            return false;
        }

        var (passwordValid, _) = PasswordHasher.ValidateComplexity(request.NuevaPassword);
        if (!passwordValid)
        {
            _logger.LogWarning("Password change with weak password for user: {UserId}", userId);
            return false;
        }

        var update = Builders<UsuarioWeb>.Update
            .Set(u => u.PasswordHash, PasswordHasher.Hash(request.NuevaPassword));

        await _db.UsuariosWeb.UpdateOneAsync(u => u.Id == userId, update);
        _logger.LogInformation("Password changed successfully for user: {UserId}", userId);

        return true;
    }

    // ── Token Revocation ──────────────────────────────────

    public async Task RevokeTokenAsync(string jti, DateTime expiresAt)
    {
        await _db.TokenBlacklist.InsertOneAsync(new TokenBlacklist
        {
            Jti = jti,
            ExpiresAt = expiresAt
        });
        _logger.LogInformation("Token revoked: {Jti}", jti);
    }

    public async Task<bool> IsTokenRevokedAsync(string jti)
    {
        var blacklisted = await _db.FindFirstOrDefaultAsync(_db.TokenBlacklist, t => t.Jti == jti);
        return blacklisted != null;
    }

    // ── Helpers ────────────────────────────────────────────

    internal string GenerateToken(string id, string email, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, id),
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

    internal string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string RandomNumberString(int length)
    {
        var numbers = new char[length];
        for (int i = 0; i < length; i++)
            numbers[i] = (char)RandomNumberGenerator.GetInt32('0', '9' + 1);
        return new string(numbers);
    }

    private static string GenerateRandomToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_");
    }

    private async Task<(string? email, string? sub)> ValidarTokenGoogleAsync(string idToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(idToken)}");

            if (!response.IsSuccessStatusCode) return (null, null);

            var json = await response.Content.ReadAsStringAsync();
            var claims = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            if (claims == null) return (null, null);

            if (!claims.TryGetValue("iss", out var issObj) || issObj is not string iss
                || iss is not ("accounts.google.com" or "https://accounts.google.com"))
            {
                return (null, null);
            }

            if (!claims.TryGetValue("email", out var emailObj) || emailObj is not string email
                || !claims.TryGetValue("email_verified", out var verifiedObj)
                || verifiedObj is not string verified || verified != "true")
            {
                return (null, null);
            }

            if (!string.IsNullOrEmpty(_googleClientId)
                && claims.TryGetValue("aud", out var audObj) && audObj is string aud
                && aud != _googleClientId)
            {
                return (null, null);
            }

            claims.TryGetValue("sub", out var subObj);
            var sub = subObj as string;

            return (email, sub);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Google token");
            return (null, null);
        }
    }
}

// ── PBKDF2 Password Hasher ──────────────────────────────

public static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 600_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySize);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public static bool Verify(string password, string hash)
    {
        var parts = hash.Split('.', 3);
        if (parts.Length != 3) return false;
        if (!int.TryParse(parts[0], out var iterations)) return false;

        var salt = Convert.FromBase64String(parts[1]);
        var key = Convert.FromBase64String(parts[2]);
        var computed = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, key.Length);

        return CryptographicOperations.FixedTimeEquals(computed, key);
    }

    public static (bool valid, string error) ValidateComplexity(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8)
            return (false, "La contraseña debe tener al menos 8 caracteres");
        if (!password.Any(char.IsUpper))
            return (false, "La contraseña debe contener al menos una mayúscula");
        if (!password.Any(char.IsLower))
            return (false, "La contraseña debe contener al menos una minúscula");
        if (!password.Any(char.IsDigit))
            return (false, "La contraseña debe contener al menos un número");
        if (!password.Any(c => "!@#$%^&*()_+-=[]{}|;':\",./<>?".Contains(c)))
            return (false, "La contraseña debe contener al menos un carácter especial");
        return (true, string.Empty);
    }
}
