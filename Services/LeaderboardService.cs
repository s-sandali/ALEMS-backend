using backend.DTOs;
using backend.Repositories;

namespace backend.Services;

/// <summary>
/// Assembles the leaderboard from repository data.
/// No SQL lives here — aggregation is delegated to <see cref="IUserRepository"/>.
/// </summary>
public class LeaderboardService : ILeaderboardService
{
    private readonly IUserRepository _userRepository;

    public LeaderboardService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<LeaderboardEntryDto>> GetLeaderboardAsync(
        int currentUserId,
        int limit = 10)
    {
        // Fetch top users and current user's rank concurrently — independent queries.
        var topUsersTask  = _userRepository.GetTopUsersAsync(limit);
        var userRankTask  = _userRepository.GetUserRankAsync(currentUserId);

        await Task.WhenAll(topUsersTask, userRankTask);

        var topUsers      = (await topUsersTask).ToList();
        var currentRank   = await userRankTask;

        // Mark the current user wherever they appear in the top list.
        bool currentUserInTop = false;
        foreach (var entry in topUsers)
        {
            if (entry.UserId == currentUserId)
            {
                entry.IsCurrentUser = true;
                currentUserInTop = true;
            }
        }

        // If the current user is outside the top N, fetch their profile and append
        // them so the frontend can always render their row at the bottom.
        if (!currentUserInTop)
        {
            var currentUser = await _userRepository.GetByIdAsync(currentUserId);
            if (currentUser is not null)
            {
                topUsers.Add(new LeaderboardEntryDto
                {
                    UserId      = currentUser.UserId,
                    Username    = currentUser.Username,
                    XpTotal     = currentUser.XpTotal,
                    Rank        = currentRank,
                    IsCurrentUser = true
                });
            }
        }

        return topUsers;
    }
}
