using System.Security.Claims;
using backend.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Xunit;

namespace backend.Tests.Middleware;

/// <summary>
/// Unit tests for <see cref="RequestLoggingMiddleware"/>.
/// </summary>
public class RequestLoggingMiddlewareTests
{
    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent)
        {
            Events.Add(logEvent);
        }
    }

    private static ILogger<RequestLoggingMiddleware> CreateLogger(CollectingSink sink, out ILoggerFactory factory)
    {
        var serilogLogger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Sink(sink)
            .CreateLogger();

        factory = new LoggerFactory([new SerilogLoggerProvider(serilogLogger, dispose: true)]);
        return factory.CreateLogger<RequestLoggingMiddleware>();
    }

    [Fact(DisplayName = "Scenario 1 - Successful request: calls next and completes without error")]
    public async Task InvokeAsync_SuccessfulRequest_CallsNextDelegate()
    {
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new RequestLoggingMiddleware(next, LoggerFactory.Create(_ => { }).CreateLogger<RequestLoggingMiddleware>());
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue("the middleware must call the next delegate in the pipeline");
    }

    [Fact(DisplayName = "Scenario 2 - Exception in next: rethrows the exception")]
    public async Task InvokeAsync_NextThrows_Rethrows()
    {
        RequestDelegate next = _ => throw new InvalidOperationException("upstream error");

        var middleware = new RequestLoggingMiddleware(next, LoggerFactory.Create(_ => { }).CreateLogger<RequestLoggingMiddleware>());
        var context = new DefaultHttpContext();

        Func<Task> act = () => middleware.InvokeAsync(context);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("upstream error",
                because: "RequestLoggingMiddleware must rethrow so GlobalExceptionMiddleware can handle it");
    }

    [Fact(DisplayName = "Scenario 3 - Authenticated request: pushes JWT sub claim into UserId")]
    public async Task InvokeAsync_AuthenticatedRequest_PushesSubClaimIntoLogContext()
    {
        var sink = new CollectingSink();
        var logger = CreateLogger(sink, out var factory);
        using (factory)
        {
            RequestDelegate next = _ => Task.CompletedTask;

            var middleware = new RequestLoggingMiddleware(next, logger);
            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(
                    new ClaimsIdentity([new Claim("sub", "clerk_user_123")], authenticationType: "Bearer"))
            };

            await middleware.InvokeAsync(context);

            sink.Events.Should().ContainSingle();
            sink.Events[0].Properties.Should().ContainKey("UserId");
            sink.Events[0].Properties["UserId"].ToString().Trim('"').Should().Be("clerk_user_123");
        }
    }

    [Fact(DisplayName = "Scenario 4 - Unauthenticated request: uses anonymous UserId")]
    public async Task InvokeAsync_UnauthenticatedRequest_UsesAnonymousUserId()
    {
        var sink = new CollectingSink();
        var logger = CreateLogger(sink, out var factory);
        using (factory)
        {
            RequestDelegate next = _ => Task.CompletedTask;

            var middleware = new RequestLoggingMiddleware(next, logger);
            var context = new DefaultHttpContext();

            await middleware.InvokeAsync(context);

            sink.Events.Should().ContainSingle();
            sink.Events[0].Properties.Should().ContainKey("UserId");
            sink.Events[0].Properties["UserId"].ToString().Trim('"').Should().Be("anonymous");
        }
    }
}
