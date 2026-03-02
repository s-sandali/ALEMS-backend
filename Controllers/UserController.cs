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

    /// <summary>
    /// GET /api/users
    /// Retrieves all users (admin operation).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllUsers()
    {
        try
        {
            var users = await _userService.GetAllUsersAsync();
            return Ok(new
            {
                status = "success",
                data = users
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in GET /api/users");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status = "error",
                message = "An unexpected error occurred while retrieving users.",
                detail = ex.Message
            });
        }
    }

    /// <summary>
    /// GET /api/users/{id}
    /// Retrieves a specific user by ID (admin operation).
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetUserById(int id)
    {
        try
        {
            var user = await _userService.GetUserByIdAsync(id);
            if (user is null)
            {
                return NotFound(new
                {
                    status = "error",
                    message = $"User with ID {id} not found."
                });
            }

            return Ok(new
            {
                status = "success",
                data = user
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in GET /api/users/{id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status = "error",
                message = "An unexpected error occurred while retrieving the user.",
                detail = ex.Message
            });
        }
    }

    /// <summary>
    /// PUT /api/users/{id}
    /// Updates a user's role and active status (admin operation).
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDto dto)
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

            var result = await _userService.UpdateUserAsync(id, dto.Role, dto.IsActive.GetValueOrDefault());

            _logger.LogInformation("POST /api/users/{Id} — updated user", id);
            return Ok(new
            {
                status = "success",
                message = "User updated successfully.",
                data = result
            });
        }
        catch (KeyNotFoundException knfe)
        {
            return NotFound(new
            {
                status = "error",
                message = knfe.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in PUT /api/users/{id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status = "error",
                message = "An unexpected error occurred while updating the user.",
                detail = ex.Message
            });
        }
    }

    /// <summary>
    /// DELETE /api/users/{id}
    /// Soft deletes a user by setting is_active to false (admin operation).
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        try
        {
            var success = await _userService.DeleteUserAsync(id);

            if (!success)
            {
                return NotFound(new
                {
                    status = "error",
                    message = $"User with ID {id} not found."
                });
            }

            return NoContent(); // 204
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in DELETE /api/users/{id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status = "error",
                message = "An unexpected error occurred while deleting the user.",
                detail = ex.Message
            });
        }
    }
}
