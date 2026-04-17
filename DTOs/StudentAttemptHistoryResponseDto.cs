namespace backend.DTOs;

/// <summary>
/// Represents a paginated response of a student's quiz attempt history.
/// </summary>
public class StudentAttemptHistoryResponseDto
{
    /// <summary>The list of attempts for this page.</summary>
    public IEnumerable<UserAttemptHistoryDto> Attempts { get; set; } = new List<UserAttemptHistoryDto>();

    /// <summary>The current page number (1-indexed).</summary>
    public int Page { get; set; }

    /// <summary>The number of attempts per page.</summary>
    public int PageSize { get; set; }

    /// <summary>The total number of attempts for this user.</summary>
    public int TotalAttempts { get; set; }

    /// <summary>The total number of pages.</summary>
    public int TotalPages => (TotalAttempts + PageSize - 1) / PageSize;

    /// <summary>Whether there are more pages after this one.</summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>Whether there are pages before this one.</summary>
    public bool HasPreviousPage => Page > 1;
}
