using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Depicta.Api.Abstractions;
using Depicta.Api.Infrastructure;
using Depicta.Api.Infrastructure.WordPress;
using Depicta.Api.Security;
using Depicta.Api.Security.Auth;
using Depicta.Api.Services;
using Depicta.Api.Vision;
using Depicta.Api.Workers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

// CLI helper: `dotnet run -- hash <password>` prints a PBKDF2 password hash and exits.
// Lets you generate an admin password hash without any extra tooling.
if (args.Length >= 2 && args[0] == "hash")
{
    Console.WriteLine(PasswordHasher.Hash(args[1]));
    return;
}

var builder = WebApplication.CreateBuilder(args);

// Error tracking. Reads the DSN from config ("Sentry:Dsn" / env Sentry__Dsn). With no DSN it's a
// no-op, so local dev needs no setup; unhandled exceptions are captured automatically in prod.
builder.WebHost.UseSentry();

builder.Services.AddControllers().AddJsonOptions(o =>
{
    // Emit enums as snake_case strings (e.g. "pending_review") to match the frontend contract.
    o.JsonSerializerOptions.Converters.Add(
        new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.SnakeCaseLower));
});
builder.Services.AddEndpointsApiExplorer();

// Lightweight liveness probe for uptime monitoring (also keeps the free instance awake).
builder.Services.AddHealthChecks();

// --- Swagger with a Bearer auth button so the API can be exercised while authenticated ---
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Depicta API", Version = "v1" });
    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the token from POST /api/auth/login (no \"Bearer \" prefix).",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
    };
    c.AddSecurityDefinition("Bearer", scheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });
});

// --- Database: SQLite for local dev (zero setup), PostgreSQL for production ---
var provider = builder.Configuration["Database:Provider"] ?? "sqlite";
var conn = builder.Configuration.GetConnectionString("Depicta") ?? "Data Source=depicta.db";
builder.Services.AddDbContext<DepictaDbContext>(opt =>
{
    if (provider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
        opt.UseNpgsql(conn);
    else
        opt.UseSqlite(conn);
});

// --- Credential encryption at rest ---
// Persist the key ring to disk (configurable) and pin the application name so keys survive restarts
// and stay decryptable across instances. Without this, a restart would orphan stored credentials.
var dp = builder.Services.AddDataProtection().SetApplicationName("Depicta");
var keyPath = builder.Configuration["DataProtection:KeyPath"];
if (!string.IsNullOrWhiteSpace(keyPath))
{
    Directory.CreateDirectory(keyPath);
    dp.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
}
builder.Services.AddSingleton<ICredentialProtector, DataProtectionCredentialProtector>();

// --- Authentication (JWT admin) ---
var auth = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
if (string.IsNullOrWhiteSpace(auth.SigningKey) || Encoding.UTF8.GetByteCount(auth.SigningKey) < 32)
{
    if (builder.Environment.IsDevelopment())
    {
        // Dev convenience only: a stable, clearly-fake key so `dotnet run` works with no setup.
        // Real environments MUST supply Auth__SigningKey (see README "Security").
        auth.SigningKey = "dev-only-insecure-signing-key-change-me-0123456789";
    }
    else
    {
        throw new InvalidOperationException(
            "Auth:SigningKey must be set to at least 32 bytes in production. Provide it via the Auth__SigningKey environment variable.");
    }
}
builder.Services.AddSingleton(auth);
builder.Services.AddSingleton<TokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = auth.Issuer,
            ValidAudience = auth.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(auth.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

// Every endpoint requires an authenticated admin unless it opts out with [AllowAnonymous].
builder.Services.AddAuthorization(o =>
{
    o.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// --- Rate limiting ---
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Global limiter: a generous per-IP fixed window that stops runaway/abusive clients.
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1) }));

    // Strict limiter for login: slows password brute-forcing to a crawl.
    o.AddPolicy("login", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) }));
});

// --- WordPress client (typed HttpClient) ---
builder.Services.AddHttpClient<IWordPressClient, WordPressRestClient>(c =>
    c.Timeout = TimeSpan.FromSeconds(30));

// --- Vision provider (default: Claude), configured from appsettings ---
var visionOpts = builder.Configuration.GetSection("Vision:Claude").Get<ClaudeVisionOptions>() ?? new();
builder.Services.AddSingleton(visionOpts);
builder.Services.AddHttpClient<IVisionProvider, ClaudeVisionProvider>(c =>
    c.Timeout = TimeSpan.FromSeconds(60));

// --- Application services ---
builder.Services.AddScoped<SiteService>();
builder.Services.AddScoped<ScanService>();
builder.Services.AddScoped<MediaService>();

// --- Background analysis worker ---
builder.Services.AddSingleton<AnalysisSignal>();
builder.Services.AddSingleton(builder.Configuration.GetSection("Worker").Get<WorkerOptions>() ?? new());
builder.Services.AddHostedService<AltAnalysisWorker>();

// --- CORS ---
// Dev: any localhost port (Vite may land on 5173, 5174, …).
// Prod: an explicit allow-list from config (Cors:AllowedOrigins). Credentials are enabled so the
// browser may send the Authorization header; that requires specific origins, never "*".
const string FrontendCors = "frontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(o => o.AddPolicy(FrontendCors, p =>
{
    p.SetIsOriginAllowed(origin =>
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var u)) return false;
        if (builder.Environment.IsDevelopment() && (u.Host == "localhost" || u.Host == "127.0.0.1")) return true;
        return allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);
    })
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials();
}));

var app = builder.Build();

// Schema init: PostgreSQL (production) applies EF migrations; SQLite (local dev) uses
// EnsureCreated so `dotnet run` just works with zero setup and no migration files.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DepictaDbContext>();
    if (db.Database.IsNpgsql())
        db.Database.Migrate();
    else
        db.Database.EnsureCreated();
}

// Security response headers on every response. The Swagger UI (Development only) loads its own
// scripts/styles, so we skip the locked-down CSP for that path — otherwise the page renders blank.
app.Use(async (ctx, next) =>
{
    var isSwagger = ctx.Request.Path.StartsWithSegments("/swagger");
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["Referrer-Policy"] = "no-referrer";
    h["X-Permitted-Cross-Domain-Policies"] = "none";
    if (!isSwagger)
    {
        h["X-Frame-Options"] = "DENY";
        h["Cross-Origin-Resource-Policy"] = "same-origin";
        // The JSON API never serves HTML, so lock the CSP right down.
        h["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
    }
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection(); // dev runs plain HTTP so the local frontend can call it
    app.UseHsts();
}

app.UseCors(FrontendCors);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Public liveness endpoint — anonymous so uptime monitors (and Render) can reach it without a token.
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

// Exposed so integration tests can reference the entry-point assembly.
public partial class Program { }
