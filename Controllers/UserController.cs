using backend.DTOs;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

/// <summary>
/// Admin-only endpoints for user management (CRUD).
/// </summary>
/// <remarks>
/// All endpoints require a valid Clerk JWT with the <b>Admin</b> role.
/// </remarks>
[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
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
    /// POST /api/users — Create a new user.
    /// </summary>
    /// <remarks>Returns <b>400</b> if the e-mail address is already registered.</remarks>
    /// <response code="201">User created successfully.</response>
    /// <response code="400">Validation failed or duplicate e-mail.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
    {
        var result = await _userService.CreateUserAsync(dto.Email, dto.Username, dto.Role);

        if (result is null)
        {
            _logger.LogWarning("POST /api/users — duplicate email: {Email}", dto.Email);
            return BadRequest(new
            {
                status  = "error",
                message = $"A user with email '{dto.Email}' already exists."
            });
        }

        _logger.LogInformation("POST /api/users — created user {UserId}", result.UserId);
        return StatusCode(StatusCodes.Status201Created, new
        {
            status  = "success",
            message = "User created successfully.",
            data    = result
        });
    }

    /// <summary>
    /// GET /api/users — Retrieve all users.
    /// </summary>
    /// <response code="200">List of all users.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _userService.GetAllUsersAsync();
        return Ok(new
        {
            status = "success",
            data   = users
        });
    }

    /// <summary>
    /// GET /api/users/{id} — Retrieve a user by ID.
    /// </summary>
    /// <param name="id">The auto-increment user ID.</param>
    /// <response code="200">User found.</response>
    /// <response code="404">No user with the supplied ID.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUserById(int id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user is null)
        {
            return NotFound(new
            {
                status  = "error",
                message = $"User with ID {id} not found."
            });
        }

        return Ok(new
        {
            status = "success",
            data   = user
        });
    }

    /// <summary>
    /// PUT /api/users/{id} — Update a user's role and active status.
    /// </summary>
    /// <param name="id">The auto-increment user ID.</param>
    /// <param name="dto">Fields to update: <c>role</c> and <c>isActive</c>.</param>
    /// <response code="200">User updated successfully.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="404">No user with the supplied ID.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDto dto)
    {
        // KeyNotFoundException from the service bubbles to GlobalExceptionMiddleware → 404
        var result = await _userService.UpdateUserAsync(id, dto.Role, dto.IsActive.GetValueOrDefault());

        _logger.LogInformation("PUT /api/users/{Id} — updated user", id);
        return Ok(new
        {
            status  = "success",
            message = "User updated successfully.",
            data    = result
        });
    }

    /// <summary>
    /// DELETE /api/users/{id} — Soft-delete a user (sets is_active = false).
    /// </summary>
    /// <param name="id">The auto-increment user ID.</param>
    /// <response code="204">User soft-deleted successfully.</response>
    /// <response code="404">No user with the supplied ID.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var success = await _userService.DeleteUserAsync(id);

        if (!success)
        {
            return NotFound(new
            {
                status  = "error",
                message = $"User with ID {id} not found."
            });
        }

        return NoContent(); // 204
    }
}
