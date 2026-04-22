using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace backend.Security;

public sealed class PerfServiceTokenOptions
{
    public bool Enabled { get; set; }
    public string? Token { get; set; }
}

public sealed class PerfServiceTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "PerfServiceToken";
    private readonly IOptions<PerfServiceTokenOptions> _options;

    public PerfServiceTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        IOptions<PerfServiceTokenOptions> options)
        : base(schemeOptions, logger, encoder, clock)
    {
        _options = options;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var options = _options.Value;
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.Token))
            return Task.FromResult(AuthenticateResult.NoResult());

        if (!Request.Headers.TryGetValue("Authorization", out var authHeaders))
            return Task.FromResult(AuthenticateResult.NoResult());

        var authorization = authHeaders.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());

        var incomingToken = authorization["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(incomingToken))
            return Task.FromResult(AuthenticateResult.NoResult());

        var incomingBytes = Encoding.UTF8.GetBytes(incomingToken);
        var expectedBytes = Encoding.UTF8.GetBytes(options.Token);

        if (!CryptographicOperations.FixedTimeEquals(incomingBytes, expectedBytes))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new[]
        {
            new Claim("sub", "perf-service-token"),
            new Claim("role", "Admin"),
            new Claim(ClaimTypes.NameIdentifier, "perf-service-token"),
            new Claim(ClaimTypes.Role, "Admin"),
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
