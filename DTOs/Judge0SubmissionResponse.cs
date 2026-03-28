using System.Text.Json.Serialization;

namespace backend.DTOs;

/// <summary>
/// Internal serialization model for a Judge0 submission request body.
/// Explicit [JsonPropertyName] ensures snake_case regardless of the app's
/// global CamelCase naming policy.
/// </summary>
internal class Judge0SubmissionRequest
{
    [JsonPropertyName("source_code")]
    public string SourceCode { get; init; } = string.Empty;

    [JsonPropertyName("language_id")]
    public int LanguageId { get; init; }

    [JsonPropertyName("stdin")]
    public string Stdin { get; init; } = string.Empty;

    [JsonPropertyName("expected_output")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExpectedOutput { get; init; }
}

/// <summary>
/// Internal deserialization model for a Judge0 submission response.
/// Not exposed to API consumers.
/// </summary>
internal class Judge0SubmissionResponse
{
    [JsonPropertyName("stdout")]
    public string? Stdout { get; set; }

    [JsonPropertyName("stderr")]
    public string? Stderr { get; set; }

    [JsonPropertyName("compile_output")]
    public string? CompileOutput { get; set; }

    [JsonPropertyName("time")]
    public string? Time { get; set; }

    [JsonPropertyName("memory")]
    public int? Memory { get; set; }

    [JsonPropertyName("status")]
    public Judge0Status? Status { get; set; }
}

internal class Judge0Status
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}
