using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using TrovaBackend.Data;
using TrovaBackend.DTOs.Common;
using TrovaBackend.Models;

namespace TrovaBackend.Middleware;

// Registered globally (see Program.cs). Blocks every authenticated request
// with a 403 unless the calling user's ApprovalStatus is Approved — except
// for the handful of controllers that have to work *before* approval:
//
//   - AuthController: register/login/verify-email/verify-identity/etc. The
//     app needs to be able to log in and read GET /api/auth/me to find out
//     its own ApprovalStatus in the first place — gating that would be a
//     deadlock.
//   - CompanyDetailsController: submitting company details is the very
//     thing an admin is reviewing to decide on approval, and happens
//     immediately after identity verification, before approval exists.
//   - BankConnectionController: part of the same pre-approval onboarding
//     chain — identity verification -> company details -> bank connection
//     -> only then does the admin decide. Confirmed this is a signup step,
//     not a post-approval feature (was wrongly left off this list at
//     first, causing every /api/bank-connection/* call to 403 for a
//     still-pending user).
//   - AdminController: obviously admin-only already ([Authorize(Roles=
//     "admin")]), not part of the regular-user approval flow.
//
// Anything with [AllowAnonymous] (ping, forgot-password, reset-password)
// is skipped too — there's no user to check yet.
public class ApprovalGateFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> ExemptControllers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Auth", "CompanyDetails", "BankConnection", "Admin"
    };

    private readonly AppDbContext _db;

    public ApprovalGateFilter(AppDbContext db)
    {
        _db = db;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var endpoint = context.HttpContext.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() != null)
        {
            await next();
            return;
        }

        if (context.ActionDescriptor is ControllerActionDescriptor descriptor &&
            ExemptControllers.Contains(descriptor.ControllerName))
        {
            await next();
            return;
        }

        var userIdClaim = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
        {
            // No authenticated user on the request — [Authorize] (or its
            // absence) already governs this endpoint, nothing for us to add.
            await next();
            return;
        }

        var current = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Role, u.ApprovalStatus, u.RejectionReason })
            .FirstOrDefaultAsync();

        if (current == null)
        {
            await next();
            return;
        }

        // Admin/bank accounts are seeded directly, never go through
        // Register, and aren't part of the approval flow at all.
        if (current.Role is "admin" or "bank")
        {
            await next();
            return;
        }

        if (current.ApprovalStatus != UserApprovalStatus.Approved)
        {
            context.Result = new ObjectResult(new ApiResponse<object>
            {
                Success = false,
                Message = current.ApprovalStatus == UserApprovalStatus.Rejected
                    ? "Your account application was not approved."
                    : "Your account is still awaiting admin approval.",
                Data = new
                {
                    approvalStatus = current.ApprovalStatus,
                    rejectionReason = current.RejectionReason
                }
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        await next();
    }
}