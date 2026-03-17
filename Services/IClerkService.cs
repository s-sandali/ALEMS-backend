namespace backend.Services;

/// <summary>
/// Wraps Clerk Backend API operations.
/// </summary>
public interface IClerkService
{
    /// <summary>
    /// Sets <c>public_metadata.role</c> for the given Clerk user via
    /// <c>PATCH /v1/users/{userId}/metadata</c>.
    /// </summary>
    Task SetUserRoleAsync(string clerkUserId, string role);
}
