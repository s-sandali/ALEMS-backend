using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

/// <summary>
/// Response DTO returned from user sync operations.
/// </summary>
public class UserResponseDto
{
    public int UserId { get; set; }

    [Required]
    [StringLength(255)]
    public string ClerkUserId { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Role { get; set; } = string.Empty;

    public int XpTotal { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
