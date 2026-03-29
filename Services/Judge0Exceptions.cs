namespace backend.Services;

/// <summary>
/// Thrown when Judge0 returns HTTP 429 (daily rate limit exhausted).
/// </summary>
public class Judge0RateLimitException : Exception
{
    public Judge0RateLimitException()
        : base("Code execution daily limit reached. Please try again tomorrow.") { }
}

/// <summary>
/// Thrown when Judge0 returns HTTP 5xx or the request times out.
/// </summary>
public class Judge0UnavailableException : Exception
{
    public Judge0UnavailableException(string detail)
        : base($"Code execution service is temporarily unavailable. {detail}") { }
}
