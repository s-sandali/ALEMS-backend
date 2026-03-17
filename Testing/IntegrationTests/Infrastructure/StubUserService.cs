using backend.DTOs;
using backend.Services;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// No-op <see cref="IUserService"/> stub — prevents any database calls during tests.
/// </summary>
public sealed class StubUserService : IUserService
{
    public Task<(UserResponseDto Dto, bool IsNewUser)> SyncUserAsync(
        string clerkUserId, string email, string username)
    {
        var dto = new UserResponseDto
        {
            UserId      = 1,
            ClerkUserId = clerkUserId,
            Email       = string.IsNullOrWhiteSpace(email)    ? "test@example.com" : email,
            Username    = string.IsNullOrWhiteSpace(username) ? "testuser"         : username,
            Role        = "Student",
            IsActive    = true
        };
        return Task.FromResult((dto, true));
    }

    public Task<UserResponseDto?> CreateUserAsync(string email, string username, string role) =>
        Task.FromResult<UserResponseDto?>(null);

    public Task<IEnumerable<UserResponseDto>> GetAllUsersAsync() =>
        Task.FromResult(Enumerable.Empty<UserResponseDto>());

    public Task<UserResponseDto?> GetUserByIdAsync(int id) =>
        Task.FromResult<UserResponseDto?>(null);

    public Task<UserResponseDto?> UpdateUserAsync(int id, string role, bool isActive) =>
        Task.FromResult<UserResponseDto?>(null);

    public Task<bool> DeleteUserAsync(int id) =>
        Task.FromResult(false);
}
