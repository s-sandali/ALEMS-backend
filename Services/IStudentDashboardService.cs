using backend.DTOs;

namespace backend.Services;

/// <summary>
/// Orchestrates all data sources required to compose the student dashboard response.
/// </summary>
public interface IStudentDashboardService
{
    /// <summary>
    /// Builds a fully populated <see cref="StudentDashboardDto"/> for the given student.
    /// Aggregates XP, level progression, badges, quiz attempt history, algorithm coverage,
    /// and performance summary in a single call.
    /// </summary>
    /// <param name="userId">Internal auto-increment user ID (not the Clerk user ID).</param>
    /// <returns>
    /// The populated dashboard DTO, or <c>null</c> when no user with <paramref name="userId"/> exists.
    /// </returns>
    Task<StudentDashboardDto?> GetStudentDashboardAsync(int userId);
}
