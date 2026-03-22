using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

/// <summary>
/// Request payload for starting a stateful practice-mode simulation session.
/// </summary>
public class StartSimulationSessionRequestDto
{
    [Required]
    public string Algorithm { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public int[] Array { get; set; } = [];

    public int? Target { get; set; }
}
