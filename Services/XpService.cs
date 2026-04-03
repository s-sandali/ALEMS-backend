namespace backend.Services;

/// <summary>
/// Calculates XP rewards for learner activities.
/// </summary>
/// <remarks>
/// XP tiers by activity type:
/// <list type="table">
///   <listheader><term>Type</term><term>Easy</term><term>Medium</term><term>Hard</term></listheader>
///   <item><term>quiz</term><term>10</term><term>20</term><term>30</term></item>
///   <item><term>coding</term><term>50</term><term>100</term><term>200</term></item>
/// </list>
/// Quiz XP is awarded per correct answer (small incremental rewards).<br/>
/// Coding XP is awarded per solved problem (larger reward for end-to-end problem solving).
/// </remarks>
public class XpService : IXpService
{
    /// <inheritdoc />
    public int CalculateXP(string type, string difficulty) =>
        type.ToLowerInvariant() switch
        {
            "quiz" => difficulty.ToLowerInvariant() switch
            {
                "easy"   => 10,
                "medium" => 20,
                "hard"   => 30,
                _ => throw new ArgumentException(
                    $"Unsupported quiz difficulty '{difficulty}'. Expected easy, medium, or hard.",
                    nameof(difficulty))
            },
            "coding" => difficulty.ToLowerInvariant() switch
            {
                "easy"   => 50,
                "medium" => 100,
                "hard"   => 200,
                _ => throw new ArgumentException(
                    $"Unsupported coding difficulty '{difficulty}'. Expected easy, medium, or hard.",
                    nameof(difficulty))
            },
            _ => throw new ArgumentException(
                $"Unsupported XP activity type '{type}'. Expected quiz or coding.",
                nameof(type))
        };
}
