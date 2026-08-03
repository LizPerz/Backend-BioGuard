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
    private readonly IEmailService _emailService;

    public AuthService(IMongoDbContext db, IConfiguration config, HttpClient httpClient, ILogger<AuthService> logger, IEmailService emailService)
    {
        _db = db;
        _httpClient = httpClient;
        _logger = logger;
        _emailService = emailService;
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

    public async Task<RegisterResult> RegisterWebAsync(RegisterWebRequest request)
    {
        var exists = await _db.FindFirstOrDefaultAsync(_db.UsuariosWeb, u => u.Correo == request.Correo);
        if (exists != null)
        {
            _logger.LogWarning("Registration attempt with existing email: {Email}", request.Correo);
            return new RegisterResult(null, "El correo ya está registrado");
        }

        var plan = await _db.FindFirstOrDefaultAsync(_db.Planes, p => p.Nombre == request.PlanNombre);
        if (plan == null)
        {
            _logger.LogWarning("Registration attempt with invalid plan: {PlanNombre}", request.PlanNombre);
            return new RegisterResult(null, "El plan seleccionado no existe");
        }

        var (passwordValid, passwordError) = PasswordHasher.ValidateComplexity(request.Password);
        if (!passwordValid)
        {
            _logger.LogWarning("Registration with weak password for email: {Correo}", request.Correo);
            return new RegisterResult(null, passwordError);
        }

        var verificationCode = RandomNumberString(6);
        var codeExpiry = DateTime.UtcNow.AddMinutes(10);

        var user = new UsuarioWeb
        {
            Nombre = request.Nombre,
            ApellidoPaterno = request.ApellidoPaterno,
            ApellidoMaterno = request.ApellidoMaterno,
            Correo = request.Correo,
            PasswordHash = PasswordHasher.Hash(request.Password),
            ProveedorAuth = "local",
            PlanId = plan.Id,
            Activo = false,
            TwoFactorCode = verificationCode,
            TwoFactorExpira = codeExpiry,
            TwoFactorVerificado = false,
            FechaRegistro = DateTime.UtcNow
        };

        await _db.UsuariosWeb.InsertOneAsync(user);

        await _emailService.SendVerificationCodeAsync(user.Correo, $"{user.Nombre} {user.ApellidoPaterno}", verificationCode);

        _logger.LogInformation("User registered (pending verification): {UserId}", user.Id);

        return new RegisterResult(new AuthResponse("", user.Id, $"{user.Nombre} {user.ApellidoPaterno}", "dueno", plan.Nombre, RequiresVerification: true), null);
    }

    // ── Login Web ──────────────────────────────────────────

    public async Task<LoginResult> LoginWebAsync(LoginWebRequest request)
    {
        var user = await _db.FindFirstOrDefaultAsync(_db.UsuariosWeb, u => u.Correo == request.Correo);
        if (user == null)
        {
            _logger.LogWarning("Login attempt for non-existent user: {Email}", request.Correo);
            return new LoginResult(null, "Credenciales inválidas");
        }

        if (!user.Activo)
        {
            _logger.LogWarning("Login attempt for unverified user: {Email}", request.Correo);
            return new LoginResult(null, "Tu correo aún no ha sido verificado. Revisa tu bandeja de entrada y confirma el código de verificación.");
        }

        if (user.LockedUntil != null && user.LockedUntil > DateTime.UtcNow)
        {
            _logger.LogWarning("Login blocked - account locked until {LockedUntil}", user.LockedUntil);
            return new LoginResult(null, "Cuenta bloqueada temporalmente por intentos fallidos. Inténtalo más tarde.");
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
            return new LoginResult(null, "Credenciales inválidas");
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
            return new LoginResult(new AuthResponse("", user.Id, "", "", "", Requires2FA: true), null);
        }

        var plan = await _db.FindFirstOrDefaultAsync(_db.Planes, p => p.Id == user.PlanId);
        var role = RolWebUsuario(user);
        var token = GenerateToken(user.Id, user.Correo, role);
        var refreshToken = await CreateAndStoreRefreshTokenAsync(user.Id, role);
        _logger.LogInformation("User logged in successfully: {UserId}", user.Id);

        return new LoginResult(new AuthResponse(token, user.Id, $"{user.Nombre} {user.ApellidoPaterno}", role, plan?.Nombre ?? "Sin plan", RefreshToken: refreshToken), null);
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

        return await LoginGoogleValidadoAsync(email, sub);
    }

    internal async Task<AuthResponse?> LoginGoogleValidadoAsync(string email, string sub)
    {
        var user = await _db.FindFirstOrDefaultAsync(_db.UsuariosWeb, u => u.Correo == email);

        if (user != null && !user.Activo)
        {
            _logger.LogWarning("Google login attempt for inactive user: {Email}", email);
            return null;
        }

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
        var role = RolWebUsuario(user);
        var token = GenerateToken(user.Id, user.Correo, role);
        var refreshToken = await CreateAndStoreRefreshTokenAsync(user.Id, role);
        _logger.LogInformation("Google login successful for user: {UserId}", user.Id);

        return new AuthResponse(token, user.Id, $"{user.Nombre} {user.ApellidoPaterno}", role, userPlan?.Nombre ?? "Sin plan", RefreshToken: refreshToken);
    }

    // ── Login por Código (Móvil) ───────────────────────────

    public async Task<AuthResponse?> LoginByCodigoAsync(LoginCodigoRequest request)
    {
        var paciente = await _db.FindFirstOrDefaultAsync(_db.Pacientes, p => p.CodigoAccesoQr == request.CodigoAcceso);
        if (paciente != null)
        {
            var token = GenerateToken(paciente.Id, paciente.CodigoAccesoQr, "paciente", pacienteId: paciente.Id);
            var refreshToken = await CreateAndStoreRefreshTokenAsync(paciente.Id, "paciente");
            _logger.LogInformation("Patient login by code: {PacienteId}", paciente.Id);
            return new AuthResponse(token, paciente.Id, paciente.Nombre, "paciente", "paciente", RefreshToken: refreshToken);
        }

        var cuidador = await _db.FindFirstOrDefaultAsync(_db.Cuidadores, c => c.CodigoAccesoQr == request.CodigoAcceso);
        if (cuidador != null)
        {
            var token = GenerateToken(cuidador.Id, cuidador.CodigoAccesoQr, "cuidador");
            var refreshToken = await CreateAndStoreRefreshTokenAsync(cuidador.Id, "cuidador");
            _logger.LogInformation("Caregiver login by code: {CuidadorId}", cuidador.Id);
            return new AuthResponse(token, cuidador.Id, cuidador.Nombre, "cuidador", "cuidador", RefreshToken: refreshToken);
        }

        _logger.LogWarning("Login by code failed: code not found");
        return null;
    }

    // ── Refresh Token ──────────────────────────────────────

    public async Task<RefreshTokenResponse?> RefreshTokenAsync(RefreshTokenRequest request, string? ip = null)
    {
        var stored = await _db.FindFirstOrDefaultAsync(_db.RefreshTokens, t =>
            t.Token == request.RefreshToken);
        if (stored == null || !stored.IsActive)
        {
            _logger.LogWarning("Refresh token attempt with invalid or inactive token");
            return null;
        }

        var role = stored.Rol ?? "dueno";
        string userId;
        string userEmail;
        string userName;

        if (role == "paciente")
        {
            var paciente = await _db.FindFirstOrDefaultAsync(_db.Pacientes, p => p.Id == stored.UsuarioId);
            if (paciente == null)
            {
                _logger.LogWarning("Refresh token paciente not found: {UsuarioId}", stored.UsuarioId);
                return null;
            }
            userId = paciente.Id;
            userEmail = paciente.CodigoAccesoQr;
            userName = paciente.Nombre;
        }
        else if (role == "cuidador")
        {
            var cuidador = await _db.FindFirstOrDefaultAsync(_db.Cuidadores, c => c.Id == stored.UsuarioId);
            if (cuidador == null)
            {
                _logger.LogWarning("Refresh token cuidador not found: {UsuarioId}", stored.UsuarioId);
                return null;
            }
            userId = cuidador.Id;
            userEmail = cuidador.CodigoAccesoQr;
            userName = cuidador.Nombre;
        }
        else
        {
            var user = await _db.FindFirstOrDefaultAsync(_db.UsuariosWeb, u => u.Id == stored.UsuarioId);
            if (user == null)
            {
                _logger.LogWarning("Refresh token user not found: {UsuarioId}", stored.UsuarioId);
                return null;
            }
            role = RolWebUsuario(user);
            userId = user.Id;
            userEmail = user.Correo;
            userName = $"{user.Nombre} {user.ApellidoPaterno}";
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

        var pacienteId = role == "paciente" ? userId : null;
        await _db.RefreshTokens.InsertOneAsync(new RefreshToken
        {
            UsuarioId = userId,
            Rol = role,
            Token = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(_refreshTokenDays),
            Ip = ip
        });

        var accessToken = GenerateToken(userId, userEmail, role, pacienteId: pacienteId);
        _logger.LogInformation("Token refreshed for user: {UserId}, role: {Role}", userId, role);

        return new RefreshTokenResponse(accessToken, newRefreshToken);
    }

    public async Task RevokeRefreshTokenAsync(RefreshToken token)
    {
        var filter = Builders<RefreshToken>.Filter.Where(t =>
            t.Token == token.Token ||
            (token.ReplacedBy != null && t.Token == token.ReplacedBy));

        var update = Builders<RefreshToken>.Update.Set(t => t.RevokedAt, DateTime.UtcNow);

        await _db.RefreshTokens.UpdateManyAsync(filter, update);
    }

    // ── 2FA ────────────────────────────────────────────────

    public async Task<bool> Enviar2FAAsync(Enviar2FARequest request)
    {
        var user = await _db.FindFirstOrDefaultAsync(_db.UsuariosWeb, u => u.Correo == request.Correo);
        if (user == null)
        {
            _logger.LogWarning("2FA send attempt for non-existent user: {Email}", request.Correo);
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

        await _emailService.SendVerificationCodeAsync(user.Correo, $"{user.Nombre} {user.ApellidoPaterno}", codigo);

        return true;
    }

    public async Task<AuthResponse?> Verificar2FAAsync(Verificar2FARequest request)
    {
        var user = await _db.FindFirstOrDefaultAsync(_db.UsuariosWeb, u => u.Correo == request.Correo);
        if (user == null)
        {
            _logger.LogWarning("2FA verification attempt for non-existent user: {Email}", request.Correo);
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

        var wasInactive = !user.Activo;

        var update = Builders<UsuarioWeb>.Update
            .Set(u => u.TwoFactorCode, null)
            .Set(u => u.TwoFactorExpira, null)
            .Set(u => u.TwoFactorVerificado, true)
            .Set(u => u.Activo, true);

        await _db.UsuariosWeb.UpdateOneAsync(u => u.Id == user.Id, update);

        var plan = await _db.FindFirstOrDefaultAsync(_db.Planes, p => p.Id == user.PlanId);
        var role = RolWebUsuario(user);
        var token = GenerateToken(user.Id, user.Correo, role);
        var refreshToken = await CreateAndStoreRefreshTokenAsync(user.Id, role);
        _logger.LogInformation("2FA verified successfully for user: {UserId} (activated={WasInactive})", user.Id, wasInactive);

        return new AuthResponse(token, user.Id, $"{user.Nombre} {user.ApellidoPaterno}", role, plan?.Nombre ?? "Sin plan", RefreshToken: refreshToken);
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

        var resetLink = $"https://bioguard.app/reset-password?token={Uri.EscapeDataString(token)}";
        await _emailService.SendPasswordResetAsync(user.Correo, $"{user.Nombre} {user.ApellidoPaterno}", resetLink);

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

    public async Task<bool> RevocarTodasLasSesionesAsync(string usuarioId)
    {
        var filter = Builders<RefreshToken>.Filter.Where(t =>
            t.UsuarioId == usuarioId && t.RevokedAt == null);
        var update = Builders<RefreshToken>.Update.Set(t => t.RevokedAt, DateTime.UtcNow);
        var result = await _db.RefreshTokens.UpdateManyAsync(filter, update);
        _logger.LogInformation("All sessions revoked for user: {UsuarioId}, count: {Count}", usuarioId, result.ModifiedCount);
        return result.ModifiedCount > 0;
    }

    // ── Helpers ────────────────────────────────────────────

    private static string RolWebUsuario(UsuarioWeb user) => user.EsAdmin ? "admin" : "dueno";

    private async Task<string> CreateAndStoreRefreshTokenAsync(string userId, string role = "dueno")
    {
        var refreshToken = GenerateRefreshToken();
        await _db.RefreshTokens.InsertOneAsync(new RefreshToken
        {
            UsuarioId = userId,
            Rol = role,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(_refreshTokenDays),
        });
        _logger.LogInformation("Refresh token created for user: {UserId}", userId);
        return refreshToken;
    }

    internal string GenerateToken(string id, string email, string role, string? pacienteId = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claimsList = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, id),
            new(ClaimTypes.NameIdentifier, id),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (!string.IsNullOrEmpty(pacienteId))
            claimsList.Add(new Claim("paciente_id", pacienteId));

        var claims = claimsList.ToArray();

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
