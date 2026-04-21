using System.Security.Claims;
using System.Text.Json;
using backend.Data; // retained — DatabaseHelper is still used by UserRepository/UserService
using backend.Repositories;
using backend.Services;
using backend.Services.Simulations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.ApplicationInsights.Extensibility;
using Serilog;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;

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

// ── Application Insights connection string ────────────────────────────────
// Declared early so the UseSerilog lambda closure can capture it.
// Priority: env var → appsettings.json → absent (graceful no-op when not configured)
var appInsightsConnectionString =
    Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")
    ?? builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

// Replace default logging with Serilog (callback form so we can resolve TelemetryClient from DI)
builder.Host.UseSerilog((ctx, services, cfg) =>
{
    cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");

    // Only wire the App Insights sink when a connection string is present.
    // When absent (local dev without AI), the app continues with console-only logging.
    if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
    {
        var telemetryClient = services.GetRequiredService<Microsoft.ApplicationInsights.TelemetryClient>();
        cfg.WriteTo.ApplicationInsights(telemetryClient, TelemetryConverter.Traces);
    }
});

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

// ── Application Insights ──────────────────────────────────────────────────
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = appInsightsConnectionString;
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

        // Disable the default inbound claim type map so JWT claim names (e.g. "role",
        // "sub", "email") are preserved as-is in the ClaimsPrincipal instead of being
        // remapped to long Microsoft schema URIs. Without this, the "role" claim gets
        // stored as ClaimTypes.Role (long URI) while RoleClaimType = "role" (short),
        // causing [Authorize(Roles = "Admin")] to always fail with 403.
        options.MapInboundClaims = false;

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
builder.Services.AddHealthChecks();

// ── Dependency Injection ───────────────────────────────────────────
builder.Services.AddScoped<DatabaseHelper>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ILevelingService, LevelingService>();
builder.Services.AddScoped<IAlgorithmRepository, AlgorithmRepository>();
builder.Services.AddScoped<IAlgorithmService, AlgorithmService>();
builder.Services.AddScoped<IQuizRepository, QuizRepository>();
builder.Services.AddScoped<IQuizService, QuizService>();
builder.Services.AddScoped<IQuizQuestionRepository, QuizQuestionRepository>();
builder.Services.AddScoped<IQuizQuestionService, QuizQuestionService>();
builder.Services.AddSingleton<IXpService, XpService>();
builder.Services.AddScoped<IQuizAttemptRepository, QuizAttemptRepository>();
builder.Services.AddScoped<IQuizAttemptService, QuizAttemptService>();
builder.Services.AddScoped<IReportRepository, ReportRepository>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IReportCsvExportService, ReportCsvExportService>();
builder.Services.AddScoped<IReportPdfExportService, ReportPdfExportService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<ICodingQuestionRepository, CodingQuestionRepository>();
builder.Services.AddScoped<ICodingQuestionService, CodingQuestionService>();
builder.Services.AddScoped<IBadgeRepository, BadgeRepository>();
builder.Services.AddScoped<IBadgeService, BadgeService>();
builder.Services.AddScoped<IStudentDashboardService, StudentDashboardService>();
builder.Services.AddScoped<ILeaderboardService, LeaderboardService>();
builder.Services.AddScoped<IActivityService, ActivityService>();
builder.Services.AddScoped<IActivityHeatmapService, ActivityHeatmapService>();
builder.Services.AddScoped<ISimulationService, SimulationService>();
builder.Services.AddScoped<IAlgorithmSimulationEngine, BubbleSortSimulationEngine>();
builder.Services.AddScoped<IAlgorithmSimulationEngine, InsertionSortSimulationEngine>();
builder.Services.AddScoped<IAlgorithmSimulationEngine, SelectionSortSimulationEngine>();
builder.Services.AddScoped<IAlgorithmSimulationEngine, BinarySearchSimulationEngine>();
builder.Services.AddScoped<IAlgorithmSimulationEngine, QuickSortSimulationEngine>();
builder.Services.AddScoped<IAlgorithmSimulationEngine, HeapSortSimulationEngine>();
builder.Services.AddScoped<IAlgorithmSimulationEngine, MergeSortSimulationEngine>();
builder.Services.AddSingleton<ISimulationSessionStore, InMemorySimulationSessionStore>();

// ── Outbound HTTP Correlation ──────────────────────────────────────
// IHttpContextAccessor lets DelegatingHandlers read the current request context.
// CorrelationIdHandler is transient so each HttpClient pipeline gets its own instance.
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<backend.Middleware.CorrelationIdHandler>();

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
})
.AddHttpMessageHandler<backend.Middleware.CorrelationIdHandler>();

// ── Judge0 Code Execution Client (self-hosted) ─────────────────────
// Self-hosted Judge0 has no auth by default.
// Set JUDGE0_AUTH_TOKEN (or Judge0:AuthToken in config) only if your
// instance was started with the JUDGE0_AUTHN_HEADER option enabled.
var judge0BaseUrl = builder.Configuration["Judge0:BaseUrl"]
    ?? throw new InvalidOperationException(
        "Judge0 base URL not configured. Set Judge0:BaseUrl in appsettings.json.");

var judge0AuthToken = Environment.GetEnvironmentVariable("JUDGE0_AUTH_TOKEN")
    ?? builder.Configuration["Judge0:AuthToken"];

builder.Services.AddHttpClient<ICodeExecutionService, CodeExecutionService>(client =>
{
    client.BaseAddress = new Uri(judge0BaseUrl);
    // Only add the auth header when a token is actually configured
    if (!string.IsNullOrWhiteSpace(judge0AuthToken))
        client.DefaultRequestHeaders.Add("X-Auth-Token", judge0AuthToken);
    client.Timeout = TimeSpan.FromSeconds(15);
})
.AddHttpMessageHandler<backend.Middleware.CorrelationIdHandler>();

var app = builder.Build();
var failFastOnMigrationError = app.Environment.IsEnvironment("Test")
    || string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

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

app.UseCors("AllowFrontend");

app.UseAuthentication();   // Must come before request logging so UserId can be read from JWT

app.UseMiddleware<backend.Middleware.RequestLoggingMiddleware>();

// ── Global Exception Handler (must be early in the pipeline) ──────
app.UseMiddleware<backend.Middleware.GlobalExceptionMiddleware>();

// ── Request Logging (logs method, path, status, duration) ─────────
app.UseAuthorization();

// ── Run pending migrations ────────────────────────────────────────
// Skipped when SkipMigrations=true (set by integration test factories that
// replace DatabaseHelper with a stub — running migrations against a stub
// would crash the host before tests can make any HTTP calls).
var skipMigrations = app.Configuration["SkipMigrations"] == "true";
if (!skipMigrations)
{
    try
    {
        var logger = Log.Logger;
        logger.Information("Checking and running pending database migrations...");

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DatabaseHelper>();
            await RunPendingMigrationsAsync(db, logger, failFastOnMigrationError);
        }

        logger.Information("Migrations completed successfully.");
    }
    catch (Exception migrationEx)
    {
        Log.Error(migrationEx, "Error running migrations");
        if (failFastOnMigrationError)
            throw;
    }
}

app.MapControllers();
app.MapHealthChecks("/health");

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

// ── Helper method to run pending migrations ───────────────────────

async Task RunPendingMigrationsAsync(backend.Data.DatabaseHelper db, Serilog.ILogger logger, bool failFastOnError)
{
    try
    {
        await using var connection = await db.OpenConnectionAsync();
        
        // Check if icon_type column exists
        const string checkSql = @"
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
            AND TABLE_NAME = 'badges'
            AND COLUMN_NAME = 'icon_type';";
        
        await using var checkCmd = new MySql.Data.MySqlClient.MySqlCommand(checkSql, connection);
        var columnExists = await checkCmd.ExecuteScalarAsync();
        
        if (columnExists == null)
        {
            logger.Information("Running V009 migration: Adding badge UI styling properties...");
            
            // Execute each ALTER TABLE statement separately with proper syntax
            var migrations = new[]
            {
                "ALTER TABLE badges ADD COLUMN icon_type VARCHAR(50) DEFAULT 'star' COMMENT 'Icon type for lucide-react icons'",
                "ALTER TABLE badges ADD COLUMN icon_color VARCHAR(20) DEFAULT '#8f8f3e' COMMENT 'Icon color in hex format'",
                "ALTER TABLE badges ADD COLUMN unlock_hint VARCHAR(100) DEFAULT 'Locked' COMMENT 'Hint text for locked badges'"
            };
            
            foreach (var statement in migrations)
            {
                try
                {
                    await using var migrationCmd = new MySql.Data.MySqlClient.MySqlCommand(statement, connection);
                    await migrationCmd.ExecuteNonQueryAsync();
                    logger.Information("Executed migration: {Statement}", statement.Substring(0, Math.Min(50, statement.Length)));
                }
                catch (MySql.Data.MySqlClient.MySqlException ex) when (ex.Number == 1060) // Column already exists
                {
                    logger.Information("Column already exists, skipping: {Statement}", statement.Substring(0, Math.Min(50, statement.Length)));
                }
            }
            
            logger.Information("V009 migration completed successfully");
        }
        else
        {
            logger.Information("V009 migration already applied (icon_type column exists)");
        }
        
        logger.Information("Synchronizing default badge catalog...");
        const string syncDefaultBadgesSql = @"
            INSERT INTO badges (badge_name, badge_description, xp_threshold, icon_type, icon_color, unlock_hint)
            VALUES
                ('First Steps', 'Earned after reaching 50 XP.', 50, 'star', '#f6c945', 'Reach 50 XP'),
                ('Quick Learner', 'Earned after reaching 150 XP.', 150, 'bolt', '#7df9ff', 'Reach 150 XP'),
                ('Problem Solver', 'Earned after reaching 300 XP.', 300, 'shield', '#7fe7a2', 'Reach 300 XP'),
                ('Algorithm Ace', 'Earned after reaching 600 XP.', 600, 'trophy', '#c8ff3e', 'Reach 600 XP'),
                ('Big O Master', 'Earned after reaching 1000 XP.', 1000, 'gauge', '#ff9f5a', 'Reach 1000 XP')
            ON DUPLICATE KEY UPDATE
                badge_description = VALUES(badge_description),
                xp_threshold = VALUES(xp_threshold),
                icon_type = VALUES(icon_type),
                icon_color = VALUES(icon_color),
                unlock_hint = VALUES(unlock_hint);";

        await using var syncDefaultBadgesCmd = new MySql.Data.MySqlClient.MySqlCommand(syncDefaultBadgesSql, connection);
        var rowsAffected = await syncDefaultBadgesCmd.ExecuteNonQueryAsync();
        logger.Information("Default badge catalog synchronized. Rows affected: {RowCount}", rowsAffected);
    }
    catch (Exception ex)
    {
        logger.Error(ex, "Error running migrations. Application may not function properly without the badge UI styling columns.");
        if (failFastOnError)
            throw;
    }
}

// Expose Program to the integration-test project (WebApplicationFactory<Program>)
public partial class Program
{
    protected Program() { }
}
