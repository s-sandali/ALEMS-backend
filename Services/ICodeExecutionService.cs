using backend.DTOs;

namespace backend.Services;

/// <summary>
/// Defines operations for executing user code via the Judge0 API.
/// </summary>
public interface ICodeExecutionService
{
    /// <summary>
    /// Submits source code to Judge0 and returns the execution result.
    /// Uses Judge0's synchronous <c>wait=true</c> mode — blocks until execution completes.
    /// Throws <see cref="ArgumentException"/> if the language ID is not supported.
    /// Throws <see cref="Judge0RateLimitException"/> if the daily quota is exhausted.
    /// Throws <see cref="Judge0UnavailableException"/> if Judge0 is unreachable.
    /// </summary>
    Task<CodeExecutionResultDto> ExecuteAsync(CodeExecutionRequestDto request, CancellationToken ct = default);

    /// <summary>
    /// Returns the static list of languages supported by this service.
    /// </summary>
    IReadOnlyList<SupportedLanguageDto> GetSupportedLanguages();
}
