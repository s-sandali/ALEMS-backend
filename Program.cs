using System.Security.Claims;
using System.Text.Json;
using backend.Data;
using backend.Repositories;
using backend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ── Services ───────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins("http://localhost:5173", "http://localhost:5174") // Allow Vite dev servers
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter your Clerk JWT token"
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ── Clerk JWT Authentication ───────────────────────────────────────
// Priority: Environment variable → appsettings.json
var clerkAuthority = Environment.GetEnvironmentVariable("CLERK_AUTHORITY")
    ?? builder.Configuration["Clerk:Authority"]
    ?? throw new InvalidOperationException(
        "Clerk authority is not configured. Set the CLERK_AUTHORITY environment variable " +
        "or Clerk:Authority in appsettings.json.");

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
            ClockSkew = TimeSpan.FromSeconds(30)
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
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;
                if (principal == null) return;
                
                var clerkUserId = principal.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier) 
                                  ?? principal.FindFirstValue("sub");
                
                if (string.IsNullOrEmpty(clerkUserId)) return;

                // Look up the user's role in the MySQL database
                var dbHelper = context.HttpContext.RequestServices.GetRequiredService<DatabaseHelper>();
                await using var connection = await dbHelper.OpenConnectionAsync();
                
                const string sql = "SELECT Role FROM Users WHERE ClerkUserId = @ClerkId LIMIT 1";
                await using var cmd = new MySql.Data.MySqlClient.MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@ClerkId", clerkUserId);
                
                var role = await cmd.ExecuteScalarAsync() as string;
                
                if (!string.IsNullOrEmpty(role))
                {
                    // Add the Role claim so [Authorize(Roles = "Admin")] works correctly
                    var identity = (System.Security.Claims.ClaimsIdentity)principal.Identity!;
                    identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role));
                }
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

var app = builder.Build();

// ── Middleware Pipeline ────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowFrontend");

app.UseAuthentication();   // Must come before UseAuthorization
app.UseAuthorization();

app.MapControllers();

app.Run();
