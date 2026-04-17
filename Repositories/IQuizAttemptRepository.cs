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
    /// Retrieves all quiz attempts across all users and quizzes.
    /// </summary>
    Task<IEnumerable<QuizAttempt>> GetAllAsync();

    /// <summary>
    /// Retrieves paginated quiz attempts for a specific user, ordered by completed_at descending (newest first).
    /// </summary>
    /// <param name="userId">The user ID to filter by.</param>
    /// <param name="pageNumber">The page number (1-indexed).</param>
    /// <param name="pageSize">The number of attempts per page.</param>
    /// <returns>A tuple containing the attempts and the total count of all attempts for the user.</returns>
    Task<(IEnumerable<QuizAttempt> Attempts, int TotalCount)> GetAttemptsForUserAsync(int userId, int pageNumber, int pageSize);
}
