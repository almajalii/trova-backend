using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrovaAPI.DTOs.Auth;
using TrovaAPI.DTOs.Common;
using TrovaAPI.Services.Auth;

namespace TrovaAPI.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    // GET /api/auth/ping
    [HttpGet("ping")]
    public IActionResult Ping() => Ok("Trova API is running");

    // POST /api/auth/register
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);
        return StatusCode(201, new ApiResponse<AuthResponse>
        {
            Success = true,
            Message = "Account created successfully.",
            Data = result
        });
    }

    // POST /api/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        return Ok(new ApiResponse<AuthResponse>
        {
            Success = true,
            Message = "Login successful.",
            Data = result
        });
    }

    // GET /api/auth/me
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var user = await _authService.GetProfileAsync(userId);
        return Ok(new ApiResponse<UserDto>
        {
            Success = true,
            Message = "Profile retrieved successfully.",
            Data = user
        });
    }

    // POST /api/auth/change-password
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        await _authService.ChangePasswordAsync(userId, request);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Password changed successfully."
        });
    }
}
