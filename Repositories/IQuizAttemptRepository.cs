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
    /// Persists the attempt, answers, and awarded XP in a single transaction.
    /// </summary>
    Task<QuizAttempt> CreateAttemptWithAnswersAndAwardXpAsync(
        QuizAttempt attempt,
        IEnumerable<AttemptAnswer> answers,
        int xpEarned);
}
