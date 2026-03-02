using backend.Infrastructure.Swagger;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using Moq;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace UnitTests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="AuthorizeOperationFilter"/>.
///
/// Scenarios covered
/// -----------------
///   1. Action-level [AllowAnonymous]      → security requirement NOT added
///   2. Controller-level [AllowAnonymous]  → security requirement NOT added ([AllowAnonymous] wins)
///   3. Action-level [Authorize]           → Bearer requirement + 401/403 responses added
///   4. Controller-level [Authorize]       → Bearer requirement + 401/403 responses added
///   5. No auth attribute                  → security requirement NOT added
/// </summary>
public class AuthorizeOperationFilterTests
{
    // ── Test endpoint stubs used to supply MethodInfo via reflection ──────────

    [AllowAnonymous]
    private static void AnonAction() { }

    [Authorize]
    private static void AuthorizedAction() { }

    private static void NoAttributeAction() { }

    [Authorize]
    private sealed class AuthorizedController
    {
        public void ControllerLevelAuthorizedAction() { }
    }

    [AllowAnonymous]
    private sealed class AnonController
    {
        [Authorize]
        public void AuthorizeOnActionAnonOnController() { }
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static OperationFilterContext BuildContext(System.Reflection.MethodInfo methodInfo)
    {
        var mockSchemaGen = new Mock<ISchemaGenerator>();
        return new OperationFilterContext(
            new ApiDescription(),
            mockSchemaGen.Object,
            new SchemaRepository(),
            methodInfo);
    }

    private static OpenApiOperation BuildOperation() => new()
    {
        Security  = [],
        Responses = new OpenApiResponses()
    };

    // ── Scenario 1 — Action [AllowAnonymous] → no security ───────────────────

    [Fact(DisplayName = "Scenario 1 — [AllowAnonymous] on action: security requirement is NOT added")]
    public void Apply_AllowAnonymousAction_SkipsSecurity()
    {
        var filter    = new AuthorizeOperationFilter();
        var operation = BuildOperation();
        var context   = BuildContext(typeof(AuthorizeOperationFilterTests)
                            .GetMethod(nameof(AnonAction),
                                System.Reflection.BindingFlags.NonPublic |
                                System.Reflection.BindingFlags.Static)!);

        filter.Apply(operation, context);

        operation.Security.Should().BeEmpty(
            because: "[AllowAnonymous] must prevent any security requirement from being added");
        operation.Responses.Should().NotContainKey("401");
        operation.Responses.Should().NotContainKey("403");
    }

    // ── Scenario 2 — Controller [AllowAnonymous] + action [Authorize] → no security

    [Fact(DisplayName = "Scenario 2 — Controller [AllowAnonymous] wins over action [Authorize]")]
    public void Apply_AllowAnonymousOnController_SkipsSecurity()
    {
        var filter    = new AuthorizeOperationFilter();
        var operation = BuildOperation();
        var context   = BuildContext(typeof(AnonController)
                            .GetMethod(nameof(AnonController.AuthorizeOnActionAnonOnController))!);

        filter.Apply(operation, context);

        operation.Security.Should().BeEmpty(
            because: "controller-level [AllowAnonymous] must prevent security even if action has [Authorize]");
        operation.Responses.Should().NotContainKey("401");
        operation.Responses.Should().NotContainKey("403");
    }

    // ── Scenario 3 — Action [Authorize] → Bearer + 401 + 403 ─────────────────

    [Fact(DisplayName = "Scenario 3 — [Authorize] on action: Bearer security requirement and 401/403 responses added")]
    public void Apply_AuthorizedAction_AddsBearerRequirementAndResponses()
    {
        var filter    = new AuthorizeOperationFilter();
        var operation = BuildOperation();
        var context   = BuildContext(typeof(AuthorizeOperationFilterTests)
                            .GetMethod(nameof(AuthorizedAction),
                                System.Reflection.BindingFlags.NonPublic |
                                System.Reflection.BindingFlags.Static)!);

        filter.Apply(operation, context);

        operation.Security.Should().ContainSingle(
            because: "exactly one Bearer security requirement must be added");
        operation.Security[0].Keys.Should().ContainSingle(s => s.Reference.Id == "Bearer");

        operation.Responses.Should().ContainKey("401",
            because: "a 401 response must be documented on protected operations");
        operation.Responses.Should().ContainKey("403",
            because: "a 403 response must be documented on protected operations");
    }

    // ── Scenario 4 — Controller [Authorize] (action has no attribute) ─────────

    [Fact(DisplayName = "Scenario 4 — Controller-level [Authorize]: Bearer requirement and 401/403 added")]
    public void Apply_AuthorizedController_AddsBearerRequirementAndResponses()
    {
        var filter    = new AuthorizeOperationFilter();
        var operation = BuildOperation();
        var context   = BuildContext(typeof(AuthorizedController)
                            .GetMethod(nameof(AuthorizedController.ControllerLevelAuthorizedAction))!);

        filter.Apply(operation, context);

        operation.Security.Should().ContainSingle(
            because: "controller-level [Authorize] must add a Bearer security requirement");
        operation.Responses.Should().ContainKey("401");
        operation.Responses.Should().ContainKey("403");
    }

    // ── Scenario 5 — No auth attribute → no security ─────────────────────────

    [Fact(DisplayName = "Scenario 5 — No [Authorize] or [AllowAnonymous]: security requirement is NOT added")]
    public void Apply_NoAuthAttribute_SkipsSecurity()
    {
        var filter    = new AuthorizeOperationFilter();
        var operation = BuildOperation();
        var context   = BuildContext(typeof(AuthorizeOperationFilterTests)
                            .GetMethod(nameof(NoAttributeAction),
                                System.Reflection.BindingFlags.NonPublic |
                                System.Reflection.BindingFlags.Static)!);

        filter.Apply(operation, context);

        operation.Security.Should().BeEmpty(
            because: "endpoints without [Authorize] must not have a security requirement");
        operation.Responses.Should().NotContainKey("401");
        operation.Responses.Should().NotContainKey("403");
    }
}
