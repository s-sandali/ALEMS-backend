namespace backend.DTOs;

/// <summary>
/// DTO for the student dashboard response.
/// Contains student ID, XP total, level progression, badges, quiz history,
/// algorithm coverage, and aggregate performance statistics.
/// Designed as a single-fetch payload so the frontend needs only one API call.
/// </summary>
public class StudentDashboardDto
{
    /// <summary>
    /// The student's user ID.
    /// </summary>
    public int StudentId { get; set; }

    /// <summary>
    /// Total XP earned by the student.
    /// </summary>
    public int XpTotal { get; set; }

    /// <summary>
    /// Level progression details including current level, XP thresholds, and progress percentage.
    /// Drives the XP progress bar on the frontend.
    /// </summary>
    public UserProgressionDto Progression { get; set; } = new UserProgressionDto();

    /// <summary>
    /// List of badges earned by the student, including award dates and icon.
    /// Sorted by badge unlock threshold ascending.
    /// </summary>
    public IEnumerable<EarnedBadgeDto> EarnedBadges { get; set; } = new List<EarnedBadgeDto>();

    /// <summary>
    /// List of all available badges including earned status.
    /// Used for rendering earned badges fully and locked badges as grayscale placeholders.
    /// Sorted by unlock threshold ascending.
    /// </summary>
    public IEnumerable<BadgeDashboardDto> AllBadges { get; set; } = new List<BadgeDashboardDto>();

    /// <summary>
    /// Aggregate quiz performance statistics across all attempts.
    /// Used to populate the Performance Summary stat cards.
    /// </summary>
    public PerformanceSummaryDto PerformanceSummary { get; set; } = new PerformanceSummaryDto();

    /// <summary>
    /// Full quiz attempt history for the student, ordered by completion date descending.
    /// Each entry is enriched with quiz title and algorithm name for display.
    /// </summary>
    public IEnumerable<QuizAttemptHistoryItemDto> QuizAttemptHistory { get; set; } = new List<QuizAttemptHistoryItemDto>();

    /// <summary>
    /// Per-algorithm coverage showing attempt counts, pass status, and best scores.
    /// Includes all algorithms, even those never attempted, so the frontend can show full coverage.
    /// </summary>
    public IEnumerable<AlgorithmCoverageItemDto> AlgorithmCoverage { get; set; } = new List<AlgorithmCoverageItemDto>();
}
