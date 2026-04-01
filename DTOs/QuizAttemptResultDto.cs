namespace backend.DTOs;

/// <summary>
/// Response DTO returned after a quiz attempt is submitted.
/// </summary>
public class QuizAttemptResultDto
{
    public int Score { get; set; }
    public int TotalQuestions { get; set; }
    public int XpEarned { get; set; }
    public List<QuizAttemptDetailedResultDto> DetailedResults { get; set; } = [];
}
