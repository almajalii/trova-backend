using Microsoft.EntityFrameworkCore;
using TrovaBackend.Data;
using TrovaBackend.DTOs.Auth;
using TrovaBackend.Models;
using TrovaBackend.Services;

namespace TrovaBackend.Services.Auth;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<UserDto> GetProfileAsync(Guid userId);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
    Task SendVerificationEmailAsync(Guid userId);
    Task VerifyEmailAsync(Guid userId, string code);
    Task ForgotPasswordAsync(ForgotPasswordRequest request);
    Task ResetPasswordAsync(ResetPasswordRequest request);
    Task VerifyIdentityAsync(Guid userId, VerifyIdentityRequest request);
}

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;

    public AuthService(AppDbContext db, ITokenService tokenService, IEmailService emailService)
    {
        _db = db;
        _tokenService = tokenService;
        _emailService = emailService;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var normalizedEmail = request.Email.ToLower().Trim();

        if (await _db.Users.AnyAsync(u => u.Email == normalizedEmail))
            throw new InvalidOperationException("An account with this email already exists.");

        var user = new User
        {
            Name = request.Name.Trim(),
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = "user",
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Generate and send the email verification code. Email errors don't
        // block registration — the user can always hit "Resend" from the
        // Verify Email screen if delivery fails.
        var verificationCode = new Random().Next(100000, 999999).ToString();
        user.EmailVerificationCode = verificationCode;
        user.EmailVerificationCodeExpiry = DateTime.UtcNow.AddMinutes(10);
        await _db.SaveChangesAsync();
        try
        {
            await _emailService.SendVerificationCodeAsync(user.Email, user.Name, verificationCode);
        }
        catch
        {
            // swallow — see comment above
        }

        var token = _tokenService.GenerateToken(user);
        return new AuthResponse { Token = token, User = MapToDto(user) };
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var normalizedEmail = request.Email.ToLower().Trim();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail)
            ?? throw new UnauthorizedAccessException("Invalid email or password.");

        if (user.IsBanned)
            throw new UnauthorizedAccessException("Your account has been suspended. Please contact support.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        var token = _tokenService.GenerateToken(user);
        return new AuthResponse { Token = token, User = MapToDto(user) };
    }

    public async Task<UserDto> GetProfileAsync(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");
        return MapToDto(user);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            throw new UnauthorizedAccessException("Current password is incorrect.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task SendVerificationEmailAsync(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        if (user.IsVerified)
            throw new InvalidOperationException("Email is already verified.");

        var code = new Random().Next(100000, 999999).ToString();
        user.EmailVerificationCode = code;
        user.EmailVerificationCodeExpiry = DateTime.UtcNow.AddMinutes(10);
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        try
        {
            await _emailService.SendVerificationCodeAsync(user.Email, user.Name, code);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EMAIL FAILED] Verification code for {user.Email}: {code} — {ex.Message}");
        }
    }

    public async Task VerifyEmailAsync(Guid userId, string code)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        if (user.IsVerified)
            return;

        if (user.EmailVerificationCode != code ||
            user.EmailVerificationCodeExpiry == null ||
            user.EmailVerificationCodeExpiry < DateTime.UtcNow)
            throw new InvalidOperationException("Invalid or expired verification code.");

        user.IsVerified = true;
        user.EmailVerificationCode = null;
        user.EmailVerificationCodeExpiry = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var normalizedEmail = request.Email.ToLower().Trim();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        // Always return success either way — never reveal whether an email
        // is registered (prevents account enumeration).
        if (user == null) return;

        var code = new Random().Next(100000, 999999).ToString();
        user.PasswordResetToken = code;
        user.PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(15);
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        try
        {
            await _emailService.SendPasswordResetEmailAsync(user.Email, user.Name, code);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EMAIL FAILED] Password reset code for {user.Email}: {code} — {ex.Message}");
        }
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.PasswordResetToken == request.Token &&
            u.PasswordResetTokenExpiry != null &&
            u.PasswordResetTokenExpiry > DateTime.UtcNow);

        if (user == null)
            throw new InvalidOperationException("Invalid or expired reset code.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiry = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task VerifyIdentityAsync(Guid userId, VerifyIdentityRequest request)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        // The name confirmed on the ID card (Sanad or scanned) is treated as
        // more authoritative than whatever was typed at signup.
        user.Name = request.FullName.Trim();
        user.IsIdentityVerified = true;
        user.NationalId = request.NationalId.Trim();
        user.IdentityVerificationMethod = request.Method;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    private static UserDto MapToDto(User user) => new()
    {
        Id = user.Id,
        Name = user.Name,
        Email = user.Email,
        Phone = user.Phone,
        Role = user.Role,
    };
}