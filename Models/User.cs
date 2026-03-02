namespace backend.Models;

/// <summary>
/// Domain model representing a row in the Users table.
/// </summary>
public class User
{
    public int UserId { get; set; }
    public string ClerkUserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = "Student";
    public int XpTotal { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
