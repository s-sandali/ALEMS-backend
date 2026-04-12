using backend.DTOs;
using backend.Models;

namespace backend.Repositories;

/// <summary>
/// Data-access contract for quiz attempts and per-question answers.
/// </summary>
public interface IQuizAttemptRepository
{
    /// <summary>
    /// Inserts a new quiz attempt and returns the persisted row.
    /// </summary>
    Task<QuizAttempt> CreateAttemptAsync(QuizAttempt attempt);

    /// <summary>
    /// Inserts a single answer row for an existing quiz attempt.
    /// </summary>
    Task<AttemptAnswer> CreateAnswerAsync(AttemptAnswer answer);

    /// <summary>
    /// Inserts multiple answer rows for an existing quiz attempt.
    /// </summary>
    Task<IEnumerable<AttemptAnswer>> CreateAnswersAsync(IEnumerable<AttemptAnswer> answers);

    /// <summary>
    /// Returns true if the user has already submitted at least one attempt for this quiz.
    /// Used to gate XP awards so only the first attempt earns XP.
    /// </summary>
    Task<bool> HasExistingAttemptAsync(int userId, int quizId);

    /// <summary>
    /// Atomically inserts the attempt, all answers, and increments the user's XP total
    /// in a single database transaction. Returns the persisted attempt with its generated ID.
    /// </summary>
    Task<QuizAttempt> SubmitAttemptTransactionalAsync(
        QuizAttempt attempt,
        IEnumerable<AttemptAnswer> answers,
        int xpToAward);

    /// <summary>
    /// Returns all quiz attempts for a student ordered by completion date descending.
    /// Each row is enriched with the quiz title and algorithm name for display purposes.
    /// </summary>
    Task<IEnumerable<QuizAttemptHistoryItemDto>> GetAttemptHistoryByUserIdAsync(int userId);

    /// <summary>
    /// Returns one row per algorithm showing the student's aggregate quiz performance.
    /// Algorithms the student has never attempted are included with zero counts.
    /// </summary>
    Task<IEnumerable<AlgorithmCoverageItemDto>> GetAlgorithmCoverageByUserIdAsync(int userId);

    /// <summary>
    /// Returns a single aggregate row with overall quiz performance statistics for the student.
    /// All numeric fields are zero when the student has no attempts.
    /// </summary>
    Task<PerformanceSummaryDto> GetPerformanceSummaryByUserIdAsync(int userId);
}
