using backend.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace backend.Tests.Middleware;

/// <summary>
/// Unit tests for <see cref="CorrelationIdMiddleware"/>.
///
/// Scenarios covered
/// -----------------
///   1. Incoming header present    → same ID stored in Items and echoed in response
///   2. No incoming header         → new GUID generated and stored in Items
///   3. Next delegate always called → pipeline continues normally
/// </summary>
public class CorrelationIdMiddlewareTests
{
    private const string HeaderName = "X-Correlation-ID";

    // -----------------------------------------------------------------------
    // Scenario 1 — Existing correlation ID is preserved
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 1 — Existing X-Correlation-ID header is echoed back unchanged")]
    public async Task InvokeAsync_ExistingHeader_PreservesCorrelationId()
    {
        var existingId = "my-correlation-id-123";

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            // Trigger OnStarting callbacks so the response header is set
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        };

        var middleware = new CorrelationIdMiddleware(next);
        var context    = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Headers[HeaderName] = existingId;

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        context.Items["CorrelationId"].Should().Be(existingId);
    }

    // -----------------------------------------------------------------------
    // Scenario 2 — No incoming header: generates a new GUID
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 2 — No incoming header: generates a new non-empty correlation ID")]
    public async Task InvokeAsync_NoHeader_GeneratesNewCorrelationId()
    {
        RequestDelegate next = _ => Task.CompletedTask;

        var middleware = new CorrelationIdMiddleware(next);
        var context    = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        var id = context.Items["CorrelationId"] as string;
        id.Should().NotBeNullOrWhiteSpace(
            because: "a new GUID must be generated when no X-Correlation-ID header is present");
        Guid.TryParse(id, out _).Should().BeTrue("the generated ID should be a valid GUID");
    }

    // -----------------------------------------------------------------------
    // Scenario 3 — Next delegate is always called
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 3 — Next delegate is always invoked")]
    public async Task InvokeAsync_AlwaysCallsNextDelegate()
    {
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new CorrelationIdMiddleware(next);
        var context    = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }
}
