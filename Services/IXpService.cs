namespace backend.Services;

/// <summary>
/// Calculates XP rewards for learner activities based on activity type and difficulty.
/// </summary>
/// <remarks>
/// Supported types: <c>"quiz"</c>, <c>"coding"</c>.<br/>
/// Supported difficulties: <c>"easy"</c>, <c>"medium"</c>, <c>"hard"</c>.
/// </remarks>
public interface IXpService
{
    /// <summary>
    /// Returns the XP reward for a given activity type and difficulty tier.
    /// </summary>
    /// <param name="type">Activity type — <c>"quiz"</c> or <c>"coding"</c>.</param>
    /// <param name="difficulty">Difficulty tier — <c>"easy"</c>, <c>"medium"</c>, or <c>"hard"</c>.</param>
    /// <returns>XP integer reward.</returns>
    /// <exception cref="ArgumentException">Thrown when type or difficulty is unrecognised.</exception>
    int CalculateXP(string type, string difficulty);
}
