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

    // Used for deserializing Judge0 responses (case-insensitive to be lenient).
    private static readonly JsonSerializerOptions _readOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Used for serializing Judge0 requests — no naming policy so [JsonPropertyName] wins.
    private static readonly JsonSerializerOptions _writeOptions = new()
    {
        PropertyNamingPolicy = null
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

        var body = new Judge0SubmissionRequest
        {
            SourceCode     = request.SourceCode,
            LanguageId     = request.LanguageId,
            Stdin          = request.Stdin ?? string.Empty,
            ExpectedOutput = string.IsNullOrWhiteSpace(request.ExpectedOutput)
                ? null
                : request.ExpectedOutput
        };

        var serialized = JsonSerializer.Serialize(body, _writeOptions);
        _logger.LogInformation(
            "Judge0 payload: LanguageId={LanguageId} SourceLen={Len} Json={Json}",
            request.LanguageId, request.SourceCode?.Length ?? -1, serialized);

        using var content = new StringContent(serialized, System.Text.Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsync(
                "submissions?base64_encoded=false&wait=true&fields=stdout,stderr,compile_output,status,time,memory",
                content,
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
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "Judge0 returned {Status}: {Body}", (int)response.StatusCode, errorBody);
            throw new Judge0UnavailableException(
                $"Judge0 rejected the submission (HTTP {(int)response.StatusCode}): {errorBody}");
        }

        var raw = await response.Content.ReadFromJsonAsync<Judge0SubmissionResponse>(
            _readOptions, ct);

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
