namespace backend.DTOs;

/// <summary>
/// Response DTO returned after code execution via Judge0.
/// </summary>
public class CodeExecutionResultDto
{
    public string? Stdout            { get; set; }
    public string? Stderr            { get; set; }
    public string? CompileOutput     { get; set; }
    public int     StatusId          { get; set; }
    public string  StatusDescription { get; set; } = string.Empty;
    /// <summary>Wall-clock execution time as a string in seconds, e.g. "0.042".</summary>
    public string? ExecutionTime     { get; set; }
    /// <summary>Memory used in kilobytes.</summary>
    public int?    MemoryUsed        { get; set; }
}
