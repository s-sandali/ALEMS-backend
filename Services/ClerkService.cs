using System.Net.Http.Json;

namespace backend.Services;

/// <summary>
/// Calls the Clerk Backend API to manage user metadata.
/// Requires the HttpClient to be pre-configured with the Clerk secret key
/// and base address (https://api.clerk.com).
/// </summary>
public class ClerkService : IClerkService
{
    private readonly HttpClient _http;
    private readonly ILogger<ClerkService> _logger;

    public ClerkService(HttpClient http, ILogger<ClerkService> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SetUserRoleAsync(string clerkUserId, string role)
    {
        var payload = new
        {
            public_metadata = new { role }
        };

        var response = await _http.PatchAsJsonAsync(
            $"/v1/users/{clerkUserId}/metadata", payload);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Clerk metadata PATCH failed — ClerkId={ClerkUserId}, Status={Status}, Body={Body}",
                clerkUserId, (int)response.StatusCode, body);
            throw new HttpRequestException(
                $"Clerk metadata update failed with status {(int)response.StatusCode}.");
        }

        _logger.LogInformation(
            "Clerk public_metadata.role set to '{Role}' for ClerkId={ClerkUserId}", role, clerkUserId);
    }
}
