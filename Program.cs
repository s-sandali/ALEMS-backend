using System.Security.Claims;
using System.Text.Json;
using backend.Data; // retained — DatabaseHelper is still used by UserRepository/UserService
using backend.Repositories;
using backend.Services;
using backend.Services.Simulations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;

// ── Serilog Bootstrap ─────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

try
{

var builder = WebApplication.CreateBuilder(args);

// Replace default logging with Serilog
builder.Host.UseSerilog();

// ── Services ───────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        // Override the default 400 validation response to use our standardized format
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .ToDictionary(
                    e => e.Key,
                    e => e.Value!.Errors.Select(err => err.ErrorMessage).ToArray()
                );

            var response = new
            {
                statusCode = 400,
                message = "Validation Failed",
                errors
            };

            return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(response);
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            // Read allowed origins from environment or config
            var allowedOrigins = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS")
                ?? builder.Configuration["AllowedOrigins"]
                ?? "http://localhost:5173,http://localhost:5174";

            policy.WithOrigins(allowedOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // ── API metadata ─────────────────────────────────────────────────
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title       = "BigO API",
        Version     = "v1",
        Description =
            "**Adaptive Learning & Engagement Management System** — REST API\n\n" +
            "Protected endpoints require a valid **Clerk JWT** Bearer token.\n" +
            "Click **Authorize ▶** and enter your token (without the `Bearer ` prefix — " +
            "Swashbuckle prepends it automatically)."
    });

    // ── JWT security definition ──────────────────────────────────────
    // Registers the Bearer scheme so the Authorize button appears in UI.
    // The global AddSecurityRequirement is intentionally absent — the
    // AuthorizeOperationFilter below applies the lock only to [Authorize] endpoints.
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name        = "Authorization",
        Type        = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme      = "bearer",
        BearerFormat = "JWT",
        In          = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description =
            "Enter your Clerk JWT token. " +
            "The `Bearer ` prefix is added automatically by Swagger UI."
    });

    // ── Operation filter — selective padlock icon ────────────────────
    // Adds Bearer requirement (+ 401/403 responses) only to endpoints
    // that carry [Authorize]. Leaves [AllowAnonymous] / unannotated
    // endpoints unlocked.
    options.OperationFilter<backend.Infrastructure.Swagger.AuthorizeOperationFilter>();

    // ── XML doc comments ─────────────────────────────────────────────
    // Surface the /// summary / remarks written on every controller action.
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
});

// ── Clerk JWT Authentication ───────────────────────────────────────
// Priority: Environment variable → appsettings.json
var isTestEnvironment = builder.Environment.IsEnvironment("Test");

var clerkAuthority = Environment.GetEnvironmentVariable("CLERK_AUTHORITY")
    ?? builder.Configuration["Clerk:Authority"];

if (string.IsNullOrWhiteSpace(clerkAuthority))
{
    if (isTestEnvironment)
    {
        clerkAuthority = "https://test.clerk.local";
    }
    else
    {
        throw new InvalidOperationException(
            "Clerk authority is not configured. Set the CLERK_AUTHORITY environment variable " +
            "or Clerk:Authority in appsettings.json.");
    }
}

var clerkAudience = Environment.GetEnvironmentVariable("CLERK_AUDIENCE")
    ?? builder.Configuration["Clerk:Audience"];  // optional — null disables audience check

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        // Clerk publishes its JWKS at {Authority}/.well-known/jwks.json
        options.Authority = clerkAuthority;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            // Issuer — must match Clerk authority
            ValidateIssuer = true,
            ValidIssuer    = clerkAuthority,

            // Audience — validate only when configured
            ValidateAudience = !string.IsNullOrEmpty(clerkAudience),
            ValidAudience    = clerkAudience,

            // Lifetime — reject expired tokens
            ValidateLifetime = true,

            // Signing key — validated via JWKS endpoint automatically
            ValidateIssuerSigningKey = true,

            // Allow a small clock skew (default is 5 min, tighten to 30 sec)
            ClockSkew = TimeSpan.FromSeconds(30),

            // Map the JWT "role" claim to ClaimTypes.Role so that
            // [Authorize(Roles = "Admin")] resolves directly from the token.
            // Clerk emits the role under the literal key "role" (not the long
            // Microsoft schema URI), so we must override the default mapping.
            RoleClaimType = "role"
        };

        // Structured 401 error responses
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JwtAuthentication");

                logger.LogWarning(context.Exception,
                    "JWT authentication failed: {Message}", context.Exception.Message);

                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                // Role is now carried in the JWT itself via Clerk public_metadata.
                // No database call is needed — validate presence and default if absent.
                var principal = context.Principal;
                if (principal == null) return Task.CompletedTask;

                var role = principal.FindFirstValue("role");

                if (string.IsNullOrWhiteSpace(role))
                {
                    // Log a warning: user has no role in public_metadata yet.
                    // This happens before the first /api/users/sync call sets it.
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("JwtAuthentication");

                    var sub = principal.FindFirstValue("sub") ?? "unknown";
                    logger.LogWarning(
                        "JWT for sub={Sub} has no 'role' claim — defaulting to 'User'. " +
                        "Ensure public_metadata.role is set in Clerk.", sub);

                    // Inject a default "User" role so the principal is never role-less.
                    // Also add a marker so UserSyncController knows the role came from
                    // this fallback (not from Clerk public_metadata) and must be persisted.
                    var identity = (ClaimsIdentity)principal.Identity!;
                    identity.AddClaim(new Claim("role", "User"));
                    identity.AddClaim(new Claim("role_missing", "true"));
                }

                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                // Suppress the default WWW-Authenticate body and return JSON instead
                context.HandleResponse();
                context.Response.StatusCode  = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";

                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    status  = "error",
                    message = "Authentication required. Provide a valid Clerk JWT in the Authorization header.",
                    detail  = context.ErrorDescription ?? context.Error
                });

                return context.Response.WriteAsync(payload);
            }
        };
    });

builder.Services.AddAuthorization();

// ── Dependency Injection ───────────────────────────────────────────
builder.Services.AddScoped<DatabaseHelper>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAlgorithmRepository, AlgorithmRepository>();
builder.Services.AddScoped<IAlgorithmService, AlgorithmService>();
builder.Services.AddScoped<ISimulationService, SimulationService>();
builder.Services.AddScoped<IAlgorithmSimulationEngine, BubbleSortSimulationEngine>();
builder.Services.AddScoped<IAlgorithmSimulationEngine, BinarySearchSimulationEngine>();
builder.Services.AddScoped<IAlgorithmSimulationEngine, QuickSortSimulationEngine>();
builder.Services.AddScoped<IAlgorithmSimulationEngine, HeapSortSimulationEngine>();
builder.Services.AddSingleton<ISimulationSessionStore, InMemorySimulationSessionStore>();

// ── Clerk Backend API Client ───────────────────────────────────────
// Used to set public_metadata.role on first sign-up via PATCH /v1/users/{id}/metadata.
var clerkSecretKey = Environment.GetEnvironmentVariable("CLERK_SECRET_KEY")
    ?? builder.Configuration["Clerk:SecretKey"];

if (string.IsNullOrWhiteSpace(clerkSecretKey))
{
    if (isTestEnvironment)
    {
        clerkSecretKey = "sk_test_dummy_value_for_test_environment";
    }
    else
    {
        throw new InvalidOperationException(
            "Clerk secret key not configured. Set the CLERK_SECRET_KEY environment variable " +
            "or Clerk:SecretKey in appsettings.json.");
    }
}

builder.Services.AddHttpClient<IClerkService, ClerkService>(client =>
{
    client.BaseAddress = new Uri("https://api.clerk.com");
    client.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", clerkSecretKey);
});

var app = builder.Build();

// ── Middleware Pipeline ────────────────────────────────────────────
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "ALEMS API v1");
    options.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();

// ── Correlation ID (must be first — enriches all downstream logs) ─
app.UseMiddleware<backend.Middleware.CorrelationIdMiddleware>();

// ── Global Exception Handler (must be early in the pipeline) ──────
app.UseMiddleware<backend.Middleware.GlobalExceptionMiddleware>();

// ── Request Logging (logs method, path, status, duration) ─────────
app.UseMiddleware<backend.Middleware.RequestLoggingMiddleware>();

app.UseCors("AllowFrontend");

app.UseAuthentication();   // Must come before UseAuthorization
app.UseAuthorization();

app.MapControllers();

await app.RunAsync();

}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

// Expose Program to the integration-test project (WebApplicationFactory<Program>)
public partial class Program
{
    protected Program() { }
}
