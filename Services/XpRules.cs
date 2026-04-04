namespace backend.Services;

/// <summary>
/// Centralized XP rules derived from difficulty tiers.
/// </summary>
public static class XpRules
{
    public static int GetQuizXpReward(string difficulty) => difficulty.ToLowerInvariant() switch
    {
        "easy" => 10,
        "medium" => 20,
        "hard" => 30,
        _ => throw new ArgumentException($"Unsupported quiz difficulty '{difficulty}'.", nameof(difficulty))
    };

    public static int GetCodingXpReward(string difficulty) => difficulty.ToLowerInvariant() switch
    {
        "easy" => 50,
        "medium" => 100,
        "hard" => 200,
        _ => throw new ArgumentException($"Unsupported coding difficulty '{difficulty}'.", nameof(difficulty))
    };
}
