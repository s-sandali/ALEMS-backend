using backend.DTOs;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

/// <summary>
/// Admin-only endpoints for user management.
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UserController> _logger;

    public UserController(IUserService userService, ILogger<UserController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/users
    /// Creates a new user (admin operation).
    /// Returns 201 Created or 400 if email already exists.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    status = "error",
                    message = "Validation failed.",
                    errors = ModelState
                        .Where(e => e.Value?.Errors.Count > 0)
                        .ToDictionary(
                            e => e.Key,
                            e => e.Value!.Errors.Select(err => err.ErrorMessage).ToArray())
                });
            }

            var result = await _userService.CreateUserAsync(dto.Email, dto.Username, dto.Role);

            if (result is null)
            {
                _logger.LogWarning("POST /api/users — duplicate email: {Email}", dto.Email);
                return BadRequest(new
                {
                    status = "error",
                    message = $"A user with email '{dto.Email}' already exists."
                });
            }

            _logger.LogInformation("POST /api/users — created user {UserId}", result.UserId);
            return StatusCode(StatusCodes.Status201Created, new
            {
                status = "success",
                message = "User created successfully.",
                data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in POST /api/users");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status = "error",
                message = "An unexpected error occurred while creating the user.",
                detail = ex.Message
            });
        }
    }
}
