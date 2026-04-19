namespace backend.DTOs;

/// <summary>
/// Represents a single quiz attempt from a student's attempt history.
/// Includes quiz info, algorithm, score, XP earned, and attempt date.
/// </summary>
public class UserAttemptHistoryDto
{
    /// <summary>The unique attempt ID.</summary>
    public int AttemptId { get; set; }

    /// <summary>The quiz ID.</summary>
    public int QuizId { get; set; }

    /// <summary>The quiz title.</summary>
    public string QuizTitle { get; set; } = string.Empty;

    /// <summary>The algorithm name associated with this quiz.</summary>
    public string AlgorithmName { get; set; } = string.Empty;

    /// <summary>The student's score on this attempt (0-100).</summary>
    public int Score { get; set; }

    /// <summary>The XP earned from this attempt.</summary>
    public int XpEarned { get; set; }

    /// <summary>Whether the student passed this attempt (score >= pass_score).</summary>
    public bool Passed { get; set; }

    /// <summary>The date and time the attempt was completed.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>The date and time the attempt was started.</summary>
    public DateTime StartedAt { get; set; }
}
