using System.Text.Json;
using backend.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace backend.Tests.Middleware;

/// <summary>
/// Unit tests for <see cref="GlobalExceptionMiddleware"/>.
///
/// Scenarios covered
/// -----------------
///   1. No exception thrown  → passes through, response unchanged
///   2. Exception thrown     → catches it, returns 500 JSON response
/// </summary>
public class GlobalExceptionMiddlewareTests
{
    // -----------------------------------------------------------------------
    // Scenario 1 — No exception: next delegate called, response unchanged
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 1 — No exception: pipeline passes through normally")]
    public async Task InvokeAsync_NoException_CallsNextDelegate()
    {
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new GlobalExceptionMiddleware(next, NullLogger<GlobalExceptionMiddleware>.Instance);
        var context    = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK,
            because: "no exception was thrown so the status should remain the default 200");
    }

    // -----------------------------------------------------------------------
    // Scenario 2 — Exception thrown: returns 500 with JSON body
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 2 — Exception thrown: returns 500 with JSON error body")]
    public async Task InvokeAsync_ExceptionThrown_Returns500WithJsonBody()
    {
        RequestDelegate next = _ => throw new InvalidOperationException("Something broke");

        var middleware = new GlobalExceptionMiddleware(next, NullLogger<GlobalExceptionMiddleware>.Instance);
        var context    = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        context.Response.ContentType.Should().Be("application/json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();

        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("statusCode").GetInt32().Should().Be(500);
        doc.RootElement.GetProperty("message").GetString()
           .Should().NotBeNullOrEmpty();
    }
}
