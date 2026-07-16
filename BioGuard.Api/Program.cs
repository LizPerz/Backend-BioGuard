using System.Text;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using BioGuard.Api.Config;
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
        }
    };
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
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<PacienteService>();
builder.Services.AddScoped<SensorService>();
builder.Services.AddScoped<UsuariosWebService>();
builder.Services.AddScoped<PagosService>();
builder.Services.AddScoped<CuidadorService>();
builder.Services.AddScoped<DispositivoService>();
builder.Services.AddScoped<NotificacionService>();
builder.Services.AddScoped<MLService>();
builder.Services.AddScoped<AuditoriaService>();

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
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

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
    await next();
});

app.UseAuthentication();
app.UseAuthorization();

// =============================================
// MAP ENDPOINTS
// =============================================
app.MapControllers();
app.MapHub<BioGuardHub>("/hubs/bioguard");

app.Run();

// ReSharper disable once EmptyNamespaceDeclaration
namespace BioGuard.Api
{
    public partial class Program { }
}
