using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

/// <summary>
/// Request DTO for admin-updating users (PUT /api/users/{id}).
/// </summary>
public class UpdateUserDto
{
    [Required(ErrorMessage = "Role is required.")]
    [RegularExpression("^(Student|Admin|Instructor)$",
        ErrorMessage = "Role must be 'Student', 'Admin', or 'Instructor'.")]
    public string Role { get; set; } = string.Empty;

    [Required(ErrorMessage = "IsActive is required.")]
    public bool? IsActive { get; set; }
}
