using backend.DTOs;

namespace backend.Services;

/// <summary>
/// Defines business logic for submitting quiz attempts.
/// </summary>
public interface IQuizAttemptService
{
    /// <summary>
    /// Validates and records a student's attempt for the specified quiz.
    /// </summary>
    Task<QuizAttemptResultDto> SubmitAttemptAsync(int quizId, string clerkUserId, CreateQuizAttemptDto dto);
}
