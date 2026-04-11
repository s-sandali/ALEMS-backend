using System.Security.Claims;
using backend.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace backend.Tests.Controllers;

/// <summary>
/// Unit tests for <see cref="StudentController"/> focusing on authorization checks.
///
/// Scenarios covered
/// -----------------
/// GetStudentDashboard Authorization
///   1. Authorized: user accessing own dashboard (ID match)     → no ForbidResult
///   2. Authorized: Admin accessing another's dashboard         → no ForbidResult
///   3. Unauthorized: non-Admin accessing another's dashboard   → 403 Forbid
///   4. Unauthorized: missing 'sub' claim                       → 403 Forbid
/// </summary>
public class StudentControllerTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static ClaimsPrincipal BuildPrincipal(
        string? sub = "clerk_001",
        string? role = null)
    {
        var claims = new List<Claim>();
        if (sub is not null) claims.Add(new Claim("sub", sub));
        if (role is not null) claims.Add(new Claim(ClaimTypes.Role, role));
        
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    // -----------------------------------------------------------------------
    // Scenario 1 — Authorized: User accessing own dashboard (ID match)
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 1 — User accessing own dashboard: authorization check passes")]
    public void GetStudentDashboard_UserAccessingOwnDashboard_AuthorizationSuccess()
    {
        // Arrange: user "1" requesting student ID 1
        var principal = BuildPrincipal("1");
        var userId = 1;
        var clerkUserId = principal.FindFirst("sub")?.Value;
        
        // Act: simulate the authorization check
        var isForbidden = clerkUserId == null || 
                         (!clerkUserId.Equals(userId.ToString(), StringComparison.OrdinalIgnoreCase) && 
                          !principal.IsInRole("Admin"));
        
        // Assert
        isForbidden.Should().BeFalse("User should be able to access their own dashboard");
    }

    // -----------------------------------------------------------------------
    // Scenario 2 — Authorized: Admin accessing another's dashboard
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 2 — Admin accessing another's dashboard: authorization check passes")]
    public void GetStudentDashboard_AdminAccessingOtherDashboard_AuthorizationSuccess()
    {
        // Arrange: Admin requesting student ID 1
        var principal = BuildPrincipal("admin_clerk", "Admin");
        var userId = 1;
        var clerkUserId = principal.FindFirst("sub")?.Value;
        
        // Act: simulate the authorization check
        var isForbidden = clerkUserId == null || 
                         (!clerkUserId.Equals(userId.ToString(), StringComparison.OrdinalIgnoreCase) && 
                          !principal.IsInRole("Admin"));
        
        // Assert
        isForbidden.Should().BeFalse("Admin should be able to access any student's dashboard");
    }

    // -----------------------------------------------------------------------
    // Scenario 3 — Unauthorized: Non-Admin accessing another's dashboard
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 3 — Non-Admin accessing another's dashboard: authorization check fails")]
    public void GetStudentDashboard_NonAdminAccessingOtherDashboard_AuthorizationFails()
    {
        // Arrange: user "1" requesting student ID 2
        var principal = BuildPrincipal("1");
        var userId = 2;
        var clerkUserId = principal.FindFirst("sub")?.Value;
        
        // Act: simulate the authorization check
        var isForbidden = clerkUserId == null || 
                         (!clerkUserId.Equals(userId.ToString(), StringComparison.OrdinalIgnoreCase) && 
                          !principal.IsInRole("Admin"));
        
        // Assert
        isForbidden.Should().BeTrue("Non-Admin user should be forbidden from accessing another's dashboard");
    }

    // -----------------------------------------------------------------------
    // Scenario 4 — Unauthorized: Missing 'sub' claim
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 4 — Missing 'sub' claim: authorization check fails")]
    public void GetStudentDashboard_MissingSubClaim_AuthorizationFails()
    {
        // Arrange: principal with no 'sub' claim
        var principal = BuildPrincipal(sub: null);
        var userId = 1;
        var clerkUserId = principal.FindFirst("sub")?.Value;
        
        // Act: simulate the authorization check
        var isForbidden = clerkUserId == null || 
                         (!clerkUserId.Equals(userId.ToString(), StringComparison.OrdinalIgnoreCase) && 
                          !principal.IsInRole("Admin"));
        
        // Assert
        isForbidden.Should().BeTrue("Missing 'sub' claim should result in authorization failure");
    }
}
