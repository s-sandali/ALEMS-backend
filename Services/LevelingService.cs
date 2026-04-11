namespace backend.Services;

/// <summary>
/// Service for calculating user levels and XP thresholds based on total XP.
/// Uses exponential progression: each level requires progressively more XP.
/// </summary>
public interface ILevelingService
{
    /// <summary>
    /// Calculates the current level based on total XP.
    /// </summary>
    /// <param name="xpTotal">Total XP earned by the user.</param>
    /// <returns>Current level (1-indexed).</returns>
    int CalculateLevel(int xpTotal);

    /// <summary>
    /// Calculates the minimum XP required to reach a specific level.
    /// </summary>
    /// <param name="level">The target level (1-indexed).</param>
    /// <returns>Cumulative XP threshold for that level.</returns>
    int GetXpThresholdForLevel(int level);

    /// <summary>
    /// Gets the XP for the previous level.
    /// </summary>
    /// <param name="currentLevel">The current level.</param>
    /// <returns>Cumulative XP at the start of current level.</returns>
    int GetXpForPreviousLevel(int currentLevel);

    /// <summary>
    /// Gets the XP threshold for the next level.
    /// </summary>
    /// <param name="currentLevel">The current level.</param>
    /// <returns>Cumulative XP needed to reach next level.</returns>
    int GetXpForNextLevel(int currentLevel);
}

/// <summary>
/// Implements exponential leveling progression.
/// Formula: Each level N requires (100 * N^1.5) XP.
/// Example:
///   Level 1: 100 XP
///   Level 2: 100 + 282 = 382 XP
///   Level 3: 382 + 519 = 901 XP
/// </summary>
public class LevelingService : ILevelingService
{
    /// <summary>
    /// Base XP required for level 1.
    /// </summary>
    private const int BaseXpPerLevel = 100;

    /// <summary>
    /// Maximum level cap.
    /// </summary>
    private const int MaxLevel = 100;

    /// <summary>
    /// Calculates XP required for a specific level (incremental, not cumulative).
    /// </summary>
    /// <remarks>
    /// Formula: 100 * level^1.5
    /// This creates an exponential growth where higher levels cost significantly more.
    /// </remarks>
    private static int GetXpRequiredForLevel(int level)
    {
        if (level < 1) return 0;
        
        // Ensure we don't exceed reasonable values
        if (level > MaxLevel) level = MaxLevel;
        
        // Exponential formula: base * level^1.5
        return (int)(BaseXpPerLevel * Math.Pow(level, 1.5));
    }

    /// <inheritdoc />
    public int CalculateLevel(int xpTotal)
    {
        int level = 1;
        int cumulativeXp = 0;

        while (level <= MaxLevel)
        {
            int xpForThisLevel = GetXpRequiredForLevel(level);
            if (cumulativeXp + xpForThisLevel > xpTotal)
            {
                break;
            }
            cumulativeXp += xpForThisLevel;
            level++;
        }

        return level;
    }

    /// <inheritdoc />
    public int GetXpThresholdForLevel(int level)
    {
        if (level < 1) return 0;

        int cumulativeXp = 0;
        for (int i = 1; i < level; i++)
        {
            cumulativeXp += GetXpRequiredForLevel(i);
        }

        return cumulativeXp;
    }

    /// <inheritdoc />
    public int GetXpForPreviousLevel(int currentLevel)
    {
        if (currentLevel <= 1)
        {
            return 0;
        }

        return GetXpThresholdForLevel(currentLevel);
    }

    /// <inheritdoc />
    public int GetXpForNextLevel(int currentLevel)
    {
        return GetXpThresholdForLevel(currentLevel + 1);
    }
}
