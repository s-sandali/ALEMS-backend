using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace backend.Infrastructure.Swagger;

/// <summary>
/// A Swashbuckle <see cref="IOperationFilter"/> that selectively applies the JWT Bearer
/// security requirement to Swagger operations.
///
/// Behaviour
/// ---------
/// • Endpoints decorated with <see cref="AuthorizeAttribute"/> (directly or via the
///   controller class) receive the padlock icon and a Bearer security requirement.
/// • Endpoints decorated with <see cref="AllowAnonymousAttribute"/> are left unsecured.
/// • Unannotated endpoints (no [Authorize]) are left unsecured.
/// • 401 and 403 responses are automatically appended to protected operations so that
///   HTTP response-code documentation stays accurate without manual attributes.
/// </summary>
public sealed class AuthorizeOperationFilter : IOperationFilter
{
    private static readonly OpenApiSecurityRequirement BearerRequirement = new()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    };

    /// <inheritdoc />
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Allow anonymous always wins — skip security for this operation
        var hasAllowAnonymous =
            context.MethodInfo
                   .GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true)
                   .Any() ||
            (context.MethodInfo.DeclaringType?
                    .GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true)
                    .Any() ?? false);

        if (hasAllowAnonymous)
            return;

        // Add security requirement only when [Authorize] is present on the action or controller
        var hasAuthorize =
            context.MethodInfo
                   .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                   .Any() ||
            (context.MethodInfo.DeclaringType?
                    .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                    .Any() ?? false);

        if (!hasAuthorize)
            return;

        // Attach the Bearer security scheme → renders the padlock in Swagger UI
        operation.Security.Add(BearerRequirement);

        // Document standard auth-failure responses so they appear under "Responses"
        operation.Responses.TryAdd("401", new OpenApiResponse
        {
            Description = "**Unauthorized** – A valid Clerk JWT is required."
        });

        operation.Responses.TryAdd("403", new OpenApiResponse
        {
            Description = "**Forbidden** – Authenticated but insufficient role."
        });
    }
}
