using backend.DTOs;

namespace backend.Services;

/// <summary>
/// Defines business logic for submitting quiz attempts and retrieving attempt history.
/// </summary>
public interface IQuizAttemptService
{
    /// <summary>
    /// Validates and records a student's attempt for the specified quiz.
    /// </summary>
    Task<QuizAttemptResultDto> SubmitAttemptAsync(int quizId, string clerkUserId, CreateQuizAttemptDto dto);

    /// <summary>
    /// Retrieves paginated quiz attempt history for a student with enriched data (quiz title, algorithm name, etc).
    /// </summary>
    /// <param name="userId">The student user ID.</param>
    /// <param name="pageNumber">The page number (1-indexed).</param>
    /// <param name="pageSize">The number of attempts per page.</param>
    /// <returns>A paginated response containing the student's attempt history.</returns>
    Task<StudentAttemptHistoryResponseDto> GetUserAttemptHistoryAsync(int userId, int pageNumber, int pageSize);
}
