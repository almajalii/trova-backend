using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrovaBackend.DTOs.Auth;
using TrovaBackend.DTOs.Common;
using TrovaBackend.Services.Auth;

namespace TrovaBackend.Controllers;

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

    // POST /api/auth/send-verification-email
    // Authenticated — the user is already logged in at this point, since
    // register returns a token immediately (matches VerifyEmailScreen's
    // "Resend" button, called via the same Dio client that auto-attaches
    // the saved bearer token).
    [HttpPost("send-verification-email")]
    [Authorize]
    public async Task<IActionResult> SendVerificationEmail()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        await _authService.SendVerificationEmailAsync(userId);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Verification code sent."
        });
    }

    // POST /api/auth/verify-email
    [HttpPost("verify-email")]
    [Authorize]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        await _authService.VerifyEmailAsync(userId, request.Code);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Email verified successfully."
        });
    }

    // POST /api/auth/forgot-password
    // Not authenticated — the user can't log in yet, that's the whole point.
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        await _authService.ForgotPasswordAsync(request);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "If this email is registered, a reset code has been sent."
        });
    }

    // POST /api/auth/reset-password
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        await _authService.ResetPasswordAsync(request);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Password has been reset successfully."
        });
    }
}
