using System.ComponentModel.DataAnnotations;
using backend.DTOs;
using FluentAssertions;
using Xunit;

namespace UnitTests.DTOs;

/// <summary>
/// S1-US5 — Input Validation Strategy (Unit-testing tier).
///
/// Validates that every DataAnnotations attribute on <see cref="CreateUserDto"/>
/// and <see cref="UpdateUserDto"/> fires the correct error message when a bad
/// model is provided, and that a fully-valid model produces zero errors.
///
/// Approach: pure in-process validation via
///   <see cref="Validator.TryValidateObject"/> — no HTTP, no DB, no middleware.
/// </summary>
public class DtoValidationTests
{
    // ═══════════════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Runs DataAnnotations validation on <paramref name="obj"/> and returns
    /// the collected <see cref="ValidationResult"/> list.
    /// </summary>
    private static IList<ValidationResult> Validate(object obj)
    {
        var results = new List<ValidationResult>();
        var ctx     = new ValidationContext(obj);
        Validator.TryValidateObject(obj, ctx, results, validateAllProperties: true);
        return results;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  CreateUserDto — valid
    // ═══════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-UV-01 — CreateUserDto: valid object produces no validation errors")]
    public void CreateUserDto_ValidObject_NoErrors()
    {
        var dto = new CreateUserDto
        {
            Email    = "alice@example.com",
            Username = "alice",
            Role     = "Student"
        };

        Validate(dto).Should().BeEmpty(
            because: "a fully-valid DTO must pass every annotation");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  CreateUserDto — Email field
    // ═══════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-UV-02 — CreateUserDto: missing email triggers [Required]")]
    public void CreateUserDto_MissingEmail_RequiredError()
    {
        var dto = new CreateUserDto
        {
            Email    = "",          // empty → treated as missing by [Required]
            Username = "alice",
            Role     = "Student"
        };

        var errors = Validate(dto);

        errors.Should().ContainSingle(r => r.MemberNames.Contains("Email"))
            .Which.ErrorMessage.Should().Be("Email is required.");
    }

    [Fact(DisplayName = "TC-UV-03 — CreateUserDto: bad email format triggers [EmailAddress]")]
    public void CreateUserDto_InvalidEmailFormat_EmailAddressError()
    {
        var dto = new CreateUserDto
        {
            Email    = "not-an-email",
            Username = "alice",
            Role     = "Student"
        };

        var errors = Validate(dto);

        errors.Should().Contain(r =>
            r.MemberNames.Contains("Email") &&
            r.ErrorMessage == "A valid email address is required.");
    }

    [Fact(DisplayName = "TC-UV-04 — CreateUserDto: email > 255 chars triggers [StringLength]")]
    public void CreateUserDto_EmailExceeds255Chars_StringLengthError()
    {
        var dto = new CreateUserDto
        {
            Email    = new string('a', 250) + "@b.com",   // 256 chars total
            Username = "alice",
            Role     = "Student"
        };

        var errors = Validate(dto);

        errors.Should().Contain(r =>
            r.MemberNames.Contains("Email") &&
            r.ErrorMessage == "Email must not exceed 255 characters.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  CreateUserDto — Username field
    // ═══════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-UV-05 — CreateUserDto: missing username triggers [Required]")]
    public void CreateUserDto_MissingUsername_RequiredError()
    {
        var dto = new CreateUserDto
        {
            Email    = "alice@example.com",
            Username = "",
            Role     = "Student"
        };

        var errors = Validate(dto);

        errors.Should().ContainSingle(r => r.MemberNames.Contains("Username"))
            .Which.ErrorMessage.Should().Be("Username is required.");
    }

    [Fact(DisplayName = "TC-UV-06 — CreateUserDto: username with 1 char triggers [StringLength] min")]
    public void CreateUserDto_UsernameTooShort_StringLengthError()
    {
        var dto = new CreateUserDto
        {
            Email    = "alice@example.com",
            Username = "a",             // MinimumLength = 2
            Role     = "Student"
        };

        var errors = Validate(dto);

        errors.Should().Contain(r =>
            r.MemberNames.Contains("Username") &&
            r.ErrorMessage == "Username must be between 2 and 100 characters.");
    }

    [Fact(DisplayName = "TC-UV-07 — CreateUserDto: username > 100 chars triggers [StringLength] max")]
    public void CreateUserDto_UsernameTooLong_StringLengthError()
    {
        var dto = new CreateUserDto
        {
            Email    = "alice@example.com",
            Username = new string('x', 101),    // MaxLength = 100
            Role     = "Student"
        };

        var errors = Validate(dto);

        errors.Should().Contain(r =>
            r.MemberNames.Contains("Username") &&
            r.ErrorMessage == "Username must be between 2 and 100 characters.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  CreateUserDto — Role field
    // ═══════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-UV-08 — CreateUserDto: missing role triggers [Required]")]
    public void CreateUserDto_MissingRole_RequiredError()
    {
        var dto = new CreateUserDto
        {
            Email    = "alice@example.com",
            Username = "alice",
            Role     = ""
        };

        var errors = Validate(dto);

        errors.Should().ContainSingle(r => r.MemberNames.Contains("Role"))
            .Which.ErrorMessage.Should().Be("Role is required.");
    }

    [Theory(DisplayName = "TC-UV-09 — CreateUserDto: invalid role value triggers [RegularExpression]")]
    [InlineData("Manager")]
    [InlineData("superadmin")]
    [InlineData("STUDENT")]      // case-sensitive
    [InlineData("guest")]
    public void CreateUserDto_InvalidRole_RegexError(string invalidRole)
    {
        var dto = new CreateUserDto
        {
            Email    = "alice@example.com",
            Username = "alice",
            Role     = invalidRole
        };

        var errors = Validate(dto);

        errors.Should().Contain(r =>
            r.MemberNames.Contains("Role") &&
            r.ErrorMessage == "Role must be 'Student', 'Admin', or 'Instructor'.");
    }

    [Theory(DisplayName = "TC-UV-10 — CreateUserDto: each valid role value passes regex")]
    [InlineData("Student")]
    [InlineData("Admin")]
    [InlineData("Instructor")]
    public void CreateUserDto_ValidRole_NoRoleError(string validRole)
    {
        var dto = new CreateUserDto
        {
            Email    = "alice@example.com",
            Username = "alice",
            Role     = validRole
        };

        Validate(dto).Should().NotContain(
            r => r.MemberNames.Contains("Role"),
            because: $"'{validRole}' is an accepted role");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  UpdateUserDto — valid
    // ═══════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-UV-11 — UpdateUserDto: valid object produces no validation errors")]
    public void UpdateUserDto_ValidObject_NoErrors()
    {
        var dto = new UpdateUserDto
        {
            Role     = "Instructor",
            IsActive = true
        };

        Validate(dto).Should().BeEmpty(
            because: "a fully-valid DTO must pass every annotation");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  UpdateUserDto — Role field
    // ═══════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-UV-12 — UpdateUserDto: missing role triggers [Required]")]
    public void UpdateUserDto_MissingRole_RequiredError()
    {
        var dto = new UpdateUserDto
        {
            Role     = "",
            IsActive = true
        };

        var errors = Validate(dto);

        errors.Should().ContainSingle(r => r.MemberNames.Contains("Role"))
            .Which.ErrorMessage.Should().Be("Role is required.");
    }

    [Theory(DisplayName = "TC-UV-13 — UpdateUserDto: invalid role value triggers [RegularExpression]")]
    [InlineData("Viewer")]
    [InlineData("admin")]        // case-sensitive
    [InlineData("")]             // empty string — [Required] fires first but regex also applies
    public void UpdateUserDto_InvalidRole_RegexOrRequiredError(string invalidRole)
    {
        var dto = new UpdateUserDto
        {
            Role     = invalidRole,
            IsActive = true
        };

        var errors = Validate(dto);

        errors.Should().Contain(
            r => r.MemberNames.Contains("Role"),
            because: $"'{invalidRole}' is not a valid role");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  UpdateUserDto — IsActive field
    // ═══════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-UV-14 — UpdateUserDto: null IsActive triggers [Required]")]
    public void UpdateUserDto_NullIsActive_RequiredError()
    {
        var dto = new UpdateUserDto
        {
            Role     = "Admin",
            IsActive = null          // bool? left null → [Required] fires
        };

        var errors = Validate(dto);

        errors.Should().ContainSingle(r => r.MemberNames.Contains("IsActive"))
            .Which.ErrorMessage.Should().Be("IsActive is required.");
    }
}
