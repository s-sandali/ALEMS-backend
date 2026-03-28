using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using backend.DTOs;

namespace backend.Services;

/// <summary>
/// Sends user code to Judge0 (RapidAPI) and returns the execution result.
/// Uses <c>wait=true</c> (synchronous mode) — one request, no polling.
/// The injected <see cref="HttpClient"/> must be pre-configured with the
/// Judge0 base address and RapidAPI authentication headers.
/// </summary>
public class CodeExecutionService : ICodeExecutionService
{
    private readonly HttpClient                    _http;
    private readonly ILogger<CodeExecutionService> _logger;

    /// <summary>
    /// Language IDs supported by this service.
    /// Judge0 CE language IDs: Python 3 = 71, JavaScript = 63, Java = 62, C++ = 54, C = 50.
    /// </summary>
    private static readonly IReadOnlyList<SupportedLanguageDto> _languages =
    [
        new() { LanguageId = 71, Name = "Python 3" },
        new() { LanguageId = 63, Name = "JavaScript (Node.js)" },
        new() { LanguageId = 62, Name = "Java" },
        new() { LanguageId = 54, Name = "C++ (GCC)" },
        new() { LanguageId = 50, Name = "C (GCC)" },
    ];

    private static readonly HashSet<int> _supportedIds =
        _languages.Select(l => l.LanguageId).ToHashSet();

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CodeExecutionService(HttpClient http, ILogger<CodeExecutionService> logger)
    {
        _http   = http;
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyList<SupportedLanguageDto> GetSupportedLanguages() => _languages;

    /// <inheritdoc />
    public async Task<CodeExecutionResultDto> ExecuteAsync(
        CodeExecutionRequestDto request,
        CancellationToken ct = default)
    {
        if (!_supportedIds.Contains(request.LanguageId))
            throw new ArgumentException(
                $"Language ID {request.LanguageId} is not supported. " +
                $"Supported IDs: {string.Join(", ", _supportedIds)}.");

        var body = new
        {
            source_code = request.SourceCode,
            language_id = request.LanguageId,
            stdin       = request.Stdin ?? string.Empty
        };

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync(
                "submissions?base64_encoded=false&wait=true&fields=stdout,stderr,compile_output,status,time,memory",
                body,
                ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError("Judge0 request timed out: {Message}", ex.Message);
            throw new Judge0UnavailableException("Request timed out.");
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            _logger.LogWarning("Judge0 rate limit hit — daily quota exhausted");
            throw new Judge0RateLimitException();
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Judge0 returned unexpected status {Status}", (int)response.StatusCode);
            throw new Judge0UnavailableException($"HTTP {(int)response.StatusCode}.");
        }

        var raw = await response.Content.ReadFromJsonAsync<Judge0SubmissionResponse>(
            _jsonOptions, ct);

        if (raw is null)
            throw new Judge0UnavailableException("Empty response from Judge0.");

        var status = raw.Status ?? new Judge0Status { Id = 0, Description = "Unknown" };

        _logger.LogInformation(
            "Code execution: LanguageId={LanguageId} StatusId={StatusId} Time={Time}",
            request.LanguageId, status.Id, raw.Time ?? "n/a");

        return new CodeExecutionResultDto
        {
            Stdout            = raw.Stdout,
            Stderr            = raw.Stderr,
            CompileOutput     = raw.CompileOutput,
            StatusId          = status.Id,
            StatusDescription = status.Description,
            ExecutionTime     = raw.Time,
            MemoryUsed        = raw.Memory
        };
    }
}
