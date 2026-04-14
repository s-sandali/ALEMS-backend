namespace backend.Middleware;

/// <summary>
/// A <see cref="DelegatingHandler"/> that propagates the inbound correlation ID
/// to every outbound <see cref="HttpClient"/> request via the <c>X-Correlation-ID</c> header.
/// The value is read from <c>HttpContext.Items["CorrelationId"]</c>, which is set by
/// <see cref="CorrelationIdMiddleware"/> at the start of each request.
/// </summary>
public class CorrelationIdHandler : DelegatingHandler
{
    private const string HeaderName = "X-Correlation-ID";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var correlationId = _httpContextAccessor.HttpContext?.Items["CorrelationId"] as string;

        if (!string.IsNullOrEmpty(correlationId))
            request.Headers.TryAddWithoutValidation(HeaderName, correlationId);

        return base.SendAsync(request, cancellationToken);
    }
}
