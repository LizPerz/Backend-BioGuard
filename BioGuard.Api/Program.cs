using System.Security.Claims;
using System.Text;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;
using BioGuard.Api.Config;
using BioGuard.Api.Models;
using BioGuard.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// =============================================
// MONGODB (environment variable fallback)
// =============================================
static string? FallbackIfEmpty(string? value, string? fallback)
    => string.IsNullOrWhiteSpace(value) ? fallback : value;

var mongoConnectionString = FallbackIfEmpty(builder.Configuration["ConnectionStrings:MongoDB"],
        Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING"))
    ?? throw new InvalidOperationException("MongoDB connection string not configured.");
var jwtKey = FallbackIfEmpty(builder.Configuration["Jwt:Key"],
        Environment.GetEnvironmentVariable("JWT_SECRET_KEY"))
    ?? throw new InvalidOperationException("JWT secret key not configured.");

var mongoConfig = new MongoDbConfig
{
    ConnectionString = mongoConnectionString,
    DatabaseName = "bioguard"
};
builder.Services.AddSingleton(mongoConfig);
builder.Services.AddSingleton<MongoDbContext>();
builder.Services.AddSingleton<IMongoDbContext>(sp => sp.GetRequiredService<MongoDbContext>());

// =============================================
// JWT AUTHENTICATION
// =============================================
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) &&
                path.StartsWithSegments("/hubs/bioguard"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        },
        OnTokenValidated = async context =>
        {
            try
            {
                var jti = context.Principal?.FindFirst("jti")?.Value;
                if (!string.IsNullOrEmpty(jti))
                {
                    var scope = context.HttpContext.RequestServices.CreateScope();
                    var authService = scope.ServiceProvider.GetRequiredService<AuthService>();
                    if (await authService.IsTokenRevokedAsync(jti))
                    {
                        context.Fail("Token has been revoked");
                    }
                }
            }
            catch (Exception ex)
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILogger<Program>>();
                logger.LogWarning(ex, "Error during token validation (OnTokenValidated)");
            }
        }
    };
});

builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.MapInboundClaims = false;
});

builder.Services.AddAuthorization();

// =============================================
// RATE LIMITING
// =============================================
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.EnableEndpointRateLimiting = true;
    options.StackBlockedRequests = false;
    options.HttpStatusCode = 429;
    options.GeneralRules = new List<RateLimitRule>
    {
        new RateLimitRule
        {
            Endpoint = "*",
            Period = "1m",
            Limit = 100
        },
        new RateLimitRule
        {
            Endpoint = "*:*:post",
            Period = "1m",
            Limit = 30
        },
        new RateLimitRule
        {
            Endpoint = "post:/api/Auth/login-web",
            Period = "1m",
            Limit = 5
        },
        new RateLimitRule
        {
            Endpoint = "post:/api/Auth/register",
            Period = "1m",
            Limit = 3
        },
        new RateLimitRule
        {
            Endpoint = "post:/api/Auth/2FA/enviar",
            Period = "1m",
            Limit = 3
        },
        new RateLimitRule
        {
            Endpoint = "post:/api/Auth/2FA/verificar",
            Period = "1m",
            Limit = 5
        },
        new RateLimitRule
        {
            Endpoint = "post:/api/Auth/forgot-password",
            Period = "1m",
            Limit = 3
        }
    };
});
builder.Services.Configure<ClientRateLimitOptions>(options =>
{
    options.ClientIdHeader = "X-ClientId";
    options.HttpStatusCode = 429;
});
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

// =============================================
// SIGNALR
// =============================================
builder.Services.AddSignalR();

// =============================================
// SERVICES (Dependency Injection)
// =============================================
builder.Services.AddHttpClient<AuthService>();
builder.Services.AddScoped<PacienteService>();
builder.Services.AddScoped<SensorService>();
builder.Services.AddScoped<UsuariosWebService>();
builder.Services.AddScoped<PagosService>();
builder.Services.AddScoped<CuidadorService>();
builder.Services.AddScoped<DispositivoService>();
builder.Services.AddScoped<NotificacionService>();
builder.Services.AddScoped<MLService>();
builder.Services.AddScoped<AuditoriaService>();
builder.Services.AddScoped<MedicamentoService>();
builder.Services.AddScoped<AlertaService>();

// =============================================
// CONTROLLERS + SWAGGER
// =============================================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BioGuard API",
        Version = "v1",
        Description = "API REST para el ecosistema médico IoT BioGuard"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingresa tu token JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// =============================================
// CORS (restricted origins)
// =============================================
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:3000" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("BioGuardPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .WithMethods("GET", "POST", "PUT", "DELETE")
              .WithHeaders("Authorization", "Content-Type", "Accept")
              .AllowCredentials();
    });
});

var app = builder.Build();

// =============================================
// MONGODB TTL INDEXES
// =============================================
try
{
    using var scope = app.Services.CreateScope();
    var mongoDbContext = scope.ServiceProvider.GetRequiredService<IMongoDbContext>();
    await CreateTtlIndex(mongoDbContext.LecturasSensores, "expireAt", 0);
    await CreateTtlIndex(mongoDbContext.RefreshTokens, "expires_at", 0);
    await CreateTtlIndex(mongoDbContext.TokenBlacklist, "expires_at", 0);
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning(ex, "Failed to create TTL indexes at startup");
}

// =============================================
// MIDDLEWARE PIPELINE
// =============================================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHsts();
app.UseHttpsRedirection();
app.UseCors("BioGuardPolicy");

// Rate limiting middleware
app.UseIpRateLimiting();

// Security headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self' wss: ws:; frame-ancestors 'none'");
    context.Response.Headers.Remove("X-Powered-By");
    await next();
});

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Unhandled exception: {Method} {Path}", context.Request.Method, context.Request.Path);
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Internal server error",
            message = app.Environment.IsDevelopment() ? ex.Message : "An error occurred",
            traceId = context.TraceIdentifier
        });
    }
});

app.UseAuthentication();
app.UseAuthorization();

// =============================================
// MAP ENDPOINTS
// =============================================
app.MapControllers();
app.MapHub<BioGuardHub>("/hubs/bioguard");
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// =============================================
// SEED ENDPOINT (test data insertion)
// =============================================
app.MapPost("/api/Seed/seed-all", async (IMongoDbContext db, ILogger<Program> logger) =>
{
    try
    {
        var now = DateTime.UtcNow;

        var existingPlan = await db.FindFirstOrDefaultAsync(db.Planes, p => p.Nombre == "Gratis");
        if (existingPlan == null)
        {
            await db.Planes.InsertOneAsync(new Plan
            {
                Nombre = "Gratis", Precio = 0, PrecioMoneda = "MXN",
                LimitePacientes = 1, LimiteCuidadores = 1, DiasHistorial = 7,
                GpsContinuo = false, AiConsole = false, Activo = true, Orden = 1,
                Descripcion = "Plan gratuito con 1 cuidador"
            });
            existingPlan = await db.FindFirstOrDefaultAsync(db.Planes, p => p.Nombre == "Gratis");
        }

        var testEmail = $"seed_{DateTime.UtcNow.Ticks}@bioguard.test";
        var existingUser = await db.FindFirstOrDefaultAsync(db.UsuariosWeb, u => u.Correo == testEmail);
        if (existingUser != null)
        {
            return Results.Ok(new { message = "Seed data already exists", userId = existingUser.Id });
        }

        var user = new UsuarioWeb
        {
            Nombre = "Carlos",
            ApellidoPaterno = "Martinez",
            ApellidoMaterno = "Lopez",
            Correo = testEmail,
            PasswordHash = PasswordHasher.Hash("SeedTest@123!"),
            ProveedorAuth = "local",
            PlanId = existingPlan!.Id,
            Activo = true,
            FechaRegistro = now
        };
        await db.UsuariosWeb.InsertOneAsync(user);
        logger.LogInformation("Seed user created: {UserId}", user.Id);

        var paciente = new Paciente
        {
            UsuarioWebId = user.Id,
            CodigoAccesoQr = "SEED-" + Guid.NewGuid().ToString("N")[..8].ToUpper(),
            Nombre = "Carlos Martinez Lopez",
            FechaNacimiento = new DateTime(1955, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            Biometria = new Biometria
            {
                Edad = 71, PesoKg = 78.5, EstaturaCm = 170.0,
                EsDiabetico = true, FamiliaresDiabetes = true,
                ActividadFisica = "sedentario"
            },
            PerfilCompletado = true,
            FechaRegistro = now
        };
        await db.Pacientes.InsertOneAsync(paciente);
        logger.LogInformation("Seed patient created: {PacienteId}", paciente.Id);

        var rnd = new Random(42);
        var lecturas = new List<LecturaSensor>();
        for (int i = 0; i < 50; i++)
        {
            var ts = now.AddMinutes(-i * 10);
            var isPrePico = i % 10 == 0;
            lecturas.Add(new LecturaSensor
            {
                Meta = new MetaData { PacienteId = paciente.Id, DispositivoMac = "AA:BB:CC:DD:EE:01" },
                Timestamp = ts,
                PulsoBpm = isPrePico ? rnd.Next(100, 120) : rnd.Next(65, 95),
                TemperaturaC = isPrePico ? 37.5 + rnd.NextDouble() * 0.8 : 36.2 + rnd.NextDouble() * 0.6,
                SudoracionGsr = isPrePico ? 8.0 + rnd.NextDouble() * 4.0 : 2.0 + rnd.NextDouble() * 3.0,
                ProbabilidadPico = isPrePico ? 0.75 + rnd.NextDouble() * 0.2 : 0.1 + rnd.NextDouble() * 0.3,
                ExpireAt = ts.AddDays(30)
            });
        }
        await db.LecturasSensores.InsertManyAsync(lecturas);
        logger.LogInformation("Seed {Count} sensor readings", lecturas.Count);

        var eventos = new List<EventoMetabolico>();
        var niveles = new[] { "Normal", "Pre-Pico", "Critico" };
        for (int i = 0; i < 8; i++)
        {
            var nivel = i < 4 ? "Normal" : i < 6 ? "Pre-Pico" : "Critico";
            eventos.Add(new EventoMetabolico
            {
                PacienteId = paciente.Id,
                NivelRiesgo = nivel,
                ProbabilidadMl = nivel == "Critico" ? 0.88 + rnd.NextDouble() * 0.1 :
                                 nivel == "Pre-Pico" ? 0.65 + rnd.NextDouble() * 0.15 : 0.2 + rnd.NextDouble() * 0.3,
                Descripcion = nivel == "Critico" ? "Pico detectado - glucosa elevada" :
                              nivel == "Pre-Pico" ? "Signos pre-pico identificados" : "Lectura dentro de parámetros normales",
                FechaEvento = now.AddHours(-i * 3),
                Atendida = i < 5
            });
        }
        await db.EventosMetabolicos.InsertManyAsync(eventos);
        logger.LogInformation("Seed {Count} metabolic events", eventos.Count);

        await db.TrackingGps.InsertManyAsync(new List<TrackingGps>
        {
            new() { Meta = new MetaData { PacienteId = paciente.Id, DispositivoMac = "AA:BB:CC:DD:EE:01" }, Timestamp = now.AddMinutes(-30), Ubicacion = new UbicacionGps { Coordinates = new[] { -99.1332, 19.4326 } }, EsEmergencia = false },
            new() { Meta = new MetaData { PacienteId = paciente.Id, DispositivoMac = "AA:BB:CC:DD:EE:01" }, Timestamp = now.AddMinutes(-20), Ubicacion = new UbicacionGps { Coordinates = new[] { -99.1335, 19.4328 } }, EsEmergencia = false },
            new() { Meta = new MetaData { PacienteId = paciente.Id, DispositivoMac = "AA:BB:CC:DD:EE:01" }, Timestamp = now.AddMinutes(-10), Ubicacion = new UbicacionGps { Coordinates = new[] { -99.1340, 19.4330 } }, EsEmergencia = true }
        });

        var medNames = new[] { ("Metformina", "500mg", "08:00,20:00"), ("Insulina", "10 unidades", "07:00,13:00,19:00"), ("Losartan", "50mg", "09:00") };
        foreach (var (name, dosis, horario) in medNames)
        {
            await db.Medicamentos.InsertOneAsync(new Medicamento
            {
                PacienteId = paciente.Id, Nombre = name, Dosis = dosis, Horario = horario,
                Activo = true, FechaCreacion = now.AddDays(-rnd.Next(5, 30)),
                UltimaToma = now.AddHours(-rnd.Next(1, 12))
            });
        }

        var alertas = new List<Alerta>
        {
            new() { PacienteId = paciente.Id, Tipo = "glucosa", Nivel = "critico", Titulo = "Pico de glucosa detectado", Mensaje = "Glucosa en 280 mg/dL. Se recomienda aplicar insulina.", Atendida = false, FechaCreacion = now.AddMinutes(-45), SensorData = new SensorData { PulsoBpm = 105, TemperaturaC = 37.8, SudoracionGsr = 9.2, ProbabilidadPico = 0.92 } },
            new() { PacienteId = paciente.Id, Tipo = "cardiaca", Nivel = "advertencia", Titulo = "Frecuencia cardiaca elevada", Mensaje = "Pulso en 110 bpm durante 5 minutos.", Atendida = true, FechaCreacion = now.AddHours(-6), FechaAtencion = now.AddHours(-5), SensorData = new SensorData { PulsoBpm = 110, TemperaturaC = 37.0, SudoracionGsr = 5.5, ProbabilidadPico = 0.70 } },
            new() { PacienteId = paciente.Id, Tipo = "glucosa", Nivel = "informativo", Titulo = "Medicamento pendiente", Mensaje = "Recordatorio: Tomar Metformina 500mg", Atendida = true, FechaCreacion = now.AddHours(-3), FechaAtencion = now.AddHours(-2.5) }
        };
        await db.Alertas.InsertManyAsync(alertas);

        await db.Notificaciones.InsertManyAsync(new List<Notificacion>
        {
            new() { PacienteId = paciente.Id, UsuarioWebId = user.Id, Titulo = "Pico detectado", Mensaje = "Se detectó un pico glucémico a las 14:30", Tipo = "alerta", Leida = false, FechaEnvio = now.AddMinutes(-45) },
            new() { PacienteId = paciente.Id, UsuarioWebId = user.Id, Titulo = "Medicamento tomado", Mensaje = "Metformina registrada correctamente", Tipo = "sistema", Leida = true, FechaEnvio = now.AddHours(-2) }
        });

        await db.Dispositivos.InsertOneAsync(new Dispositivo
        {
            PacienteId = paciente.Id, NombreDispositivo = "BioGuard Watch Pro",
            MacAddress = "AA:BB:CC:DD:EE:01", Conectado = true, FechaVinculacion = now.AddDays(-30)
        });

        var cuidadorUser = new UsuarioWeb
        {
            Nombre = "Maria", ApellidoPaterno = "Martinez", ApellidoMaterno = "Ruiz",
            Correo = $"cuidador_{DateTime.UtcNow.Ticks}@bioguard.test",
            PasswordHash = PasswordHasher.Hash("Cuidador@123!"),
            ProveedorAuth = "local", PlanId = existingPlan.Id, Activo = true, FechaRegistro = now
        };
        await db.UsuariosWeb.InsertOneAsync(cuidadorUser);
        await db.Cuidadores.InsertOneAsync(new Cuidador
        {
            UsuarioWebId = cuidadorUser.Id, PacienteId = paciente.Id,
            CodigoAccesoQr = "CU-" + Guid.NewGuid().ToString("N")[..8].ToUpper(),
            Nombre = "Maria Martinez Ruiz", Parentesco = "Hija", Telefono = "5551234567",
            Correo = cuidadorUser.Correo, FechaAutorizacion = now.AddDays(-15)
        });

        await db.Pagos.InsertOneAsync(new Pago
        {
            UsuarioWebId = user.Id, Monto = 0, Moneda = "MXN", PlanId = existingPlan.Id,
            Estado = "completado", FechaPago = now.AddDays(-30), MetodoPago = "gratis"
        });

        await db.ModelosMl.InsertOneAsync(new ModeloMl
        {
            Version = "1.0.0", FechaEntrenamiento = now.AddDays(-7),
            Accuracy = 0.89, Precision = 0.87, Recall = 0.91, F1Score = 0.89,
            TotalMuestras = 5000, Activo = true, Descripcion = "Modelo inicial de predicción de picos glucémicos"
        });

        await db.PrediccionesMl.InsertOneAsync(new PrediccionMl
        {
            PacienteId = paciente.Id, ProbabilidadPico = 0.72, NivelRiesgo = "Pre-Pico",
            HorasEstimadas = 4, Recomendacion = "Mantener hidratación y verificar glucosa en 2 horas",
            ModeloVersion = "1.0.0", FechaPrediccion = now.AddMinutes(-30),
            FechaExpiracion = now.AddHours(2)
        });

        return Results.Ok(new
        {
            message = "Seed data inserted successfully",
            userId = user.Id,
            pacienteId = paciente.Id,
            cuidadorUserId = cuidadorUser.Id,
            email = testEmail,
            password = "SeedTest@123!",
            stats = new
            {
                lecturas = lecturas.Count,
                eventos = eventos.Count,
                tracking = 3,
                medicamentos = medNames.Length,
                alertas = alertas.Count,
                notificaciones = 2,
                dispositivos = 1,
                cuidadores = 1,
                pagos = 1,
                modelos = 1,
                predicciones = 1
            }
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error during seed data insertion");
        return Results.Problem(ex.Message);
    }
});

app.Run();

static async Task CreateTtlIndex<T>(IMongoCollection<T> collection, string fieldName, int expirationSeconds)
{
    var indexKeys = Builders<T>.IndexKeys.Ascending(fieldName);
    var indexOptions = new CreateIndexOptions { ExpireAfter = TimeSpan.FromSeconds(expirationSeconds) };
    var indexModel = new CreateIndexModel<T>(indexKeys, indexOptions);
    await collection.Indexes.CreateOneAsync(indexModel);
}

// ReSharper disable once EmptyNamespaceDeclaration
namespace BioGuard.Api
{
    public partial class Program { }
}
