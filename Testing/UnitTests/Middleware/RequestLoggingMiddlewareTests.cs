using backend.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace backend.Tests.Middleware;

/// <summary>
/// Unit tests for <see cref="RequestLoggingMiddleware"/>.
///
/// Scenarios covered
/// -----------------
///   1. Successful request  → next delegate called, no exception rethrown
///   2. Exception in next   → exception is rethrown (not swallowed)
/// </summary>
public class RequestLoggingMiddlewareTests
{
    // -----------------------------------------------------------------------
    // Scenario 1 — Successful request: next delegate is called
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 1 — Successful request: calls next and completes without error")]
    public async Task InvokeAsync_SuccessfulRequest_CallsNextDelegate()
    {
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new RequestLoggingMiddleware(next, NullLogger<RequestLoggingMiddleware>.Instance);
        var context    = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue("the middleware must call the next delegate in the pipeline");
    }

    // -----------------------------------------------------------------------
    // Scenario 2 — Exception in next: must rethrow (not swallow)
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 2 — Exception in next: rethrows the exception")]
    public async Task InvokeAsync_NextThrows_Rethrows()
    {
        RequestDelegate next = _ => throw new InvalidOperationException("upstream error");

        var middleware = new RequestLoggingMiddleware(next, NullLogger<RequestLoggingMiddleware>.Instance);
        var context    = new DefaultHttpContext();

        Func<Task> act = () => middleware.InvokeAsync(context);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("upstream error",
                because: "RequestLoggingMiddleware must rethrow so GlobalExceptionMiddleware can handle it");
    }
}
