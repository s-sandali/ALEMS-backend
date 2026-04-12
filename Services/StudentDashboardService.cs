using backend.DTOs;
using backend.Repositories;

namespace backend.Services;

/// <summary>
/// Orchestrates all data sources required to compose the student dashboard response.
/// This service contains no SQL and no HTTP concerns — it delegates to repositories
/// and other services and assembles the result into a single <see cref="StudentDashboardDto"/>.
/// </summary>
public class StudentDashboardService : IStudentDashboardService
{
    private readonly IUserRepository _userRepository;
    private readonly ILevelingService _levelingService;
    private readonly IBadgeService _badgeService;
    private readonly IQuizAttemptRepository _quizAttemptRepository;

    public StudentDashboardService(
        IUserRepository userRepository,
        ILevelingService levelingService,
        IBadgeService badgeService,
        IQuizAttemptRepository quizAttemptRepository)
    {
        _userRepository       = userRepository;
        _levelingService      = levelingService;
        _badgeService         = badgeService;
        _quizAttemptRepository = quizAttemptRepository;
    }

    /// <inheritdoc />
    public async Task<StudentDashboardDto?> GetStudentDashboardAsync(int userId)
    {
        // ── 1. Resolve user ────────────────────────────────────────────────
        var user = await _userRepository.GetByIdAsync(userId);
        if (user is null)
            return null;

        // ── 2. Auto-award any badges the student has just become eligible for ──
        // This keeps the dashboard consistent: a student who just crossed an XP
        // threshold will see the new badge immediately on the next dashboard load.
        await _badgeService.AwardUnlockedBadgesAsync(userId);

        // ── 3. Compute XP level progression ───────────────────────────────
        var progression = BuildProgression(userId, user.XpTotal);

        // ── 4. Fetch badge data ────────────────────────────────────────────
        var earnedBadges   = await _badgeService.GetEarnedBadgesWithAwardDateAsync(userId);
        var allBadgesList  = await BuildAllBadgesDashboardAsync(userId, earnedBadges);

        // ── 5. Fetch quiz aggregations (run concurrently — independent queries) ──
        var performanceTask = _quizAttemptRepository.GetPerformanceSummaryByUserIdAsync(userId);
        var historyTask     = _quizAttemptRepository.GetAttemptHistoryByUserIdAsync(userId);
        var coverageTask    = _quizAttemptRepository.GetAlgorithmCoverageByUserIdAsync(userId);

        await Task.WhenAll(performanceTask, historyTask, coverageTask);

        // ── 6. Assemble and return ─────────────────────────────────────────
        return new StudentDashboardDto
        {
            StudentId          = userId,
            XpTotal            = user.XpTotal,
            Progression        = progression,
            EarnedBadges       = earnedBadges,
            AllBadges          = allBadgesList,
            PerformanceSummary = await performanceTask,
            QuizAttemptHistory = await historyTask,
            AlgorithmCoverage  = await coverageTask
        };
    }

    // ── Private helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="UserProgressionDto"/> from the student's current XP total.
    /// Replicates the same calculation used by the standalone /progression endpoint.
    /// </summary>
    private UserProgressionDto BuildProgression(int userId, int xpTotal)
    {
        int currentLevel    = _levelingService.CalculateLevel(xpTotal);
        int xpPrevLevel     = _levelingService.GetXpForPreviousLevel(currentLevel);
        int xpForNextLevel  = _levelingService.GetXpForNextLevel(currentLevel);
        int xpInCurrentLevel = xpTotal - xpPrevLevel;
        int xpNeededForLevel = xpForNextLevel - xpPrevLevel;
        double progressPercentage = xpNeededForLevel > 0
            ? Math.Min((xpInCurrentLevel / (double)xpNeededForLevel) * 100, 100)
            : 0;

        return new UserProgressionDto
        {
            UserId             = userId,
            XpTotal            = xpTotal,
            CurrentLevel       = currentLevel,
            XpPrevLevel        = xpPrevLevel,
            XpForNextLevel     = xpForNextLevel,
            XpInCurrentLevel   = xpInCurrentLevel,
            XpNeededForLevel   = xpNeededForLevel,
            ProgressPercentage = progressPercentage
        };
    }

    /// <summary>
    /// Fetches all available badges and annotates each with whether the student has earned it.
    /// Uses a hash-set over the already-fetched <paramref name="earnedBadges"/> for O(1) lookup.
    /// </summary>
    private async Task<IEnumerable<BadgeDashboardDto>> BuildAllBadgesDashboardAsync(
        int userId,
        IEnumerable<EarnedBadgeDto> earnedBadges)
    {
        var earnedIds = new HashSet<int>(earnedBadges.Select(b => b.Id));
        var allBadges = await _badgeService.GetAllBadgesAsync();

        return allBadges.Select(b => new BadgeDashboardDto
        {
            Id          = b.BadgeId,
            Name        = b.BadgeName,
            Description = b.BadgeDescription,
            XpThreshold = b.XpThreshold,
            IconType    = b.IconType,
            IconColor   = b.IconColor,
            UnlockHint  = b.UnlockHint,
            Earned      = earnedIds.Contains(b.BadgeId)
        }).ToList();
    }
}
