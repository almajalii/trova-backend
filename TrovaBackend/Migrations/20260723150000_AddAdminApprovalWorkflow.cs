using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrovaBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminApprovalWorkflow : Migration
    {
        // Fixed so this migration is idempotent/reproducible across
        // environments — same convention as BankUserId in
        // AddBankReviewWorkflow.
        private static readonly Guid AdminUserId = new Guid("a17b1e6d-2b8b-4b1a-9c7e-5a3f6e2d1c40");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Users: admin approval workflow ──────────────────────────
            // defaultValue: "approved" backfills every existing row
            // (including the already-seeded bank account) to Approved, so
            // no current account gets locked out — confirmed decision. New
            // signups always set this explicitly to "pending" in
            // AuthService.RegisterAsync; the SQL default here only matters
            // for this one-time backfill (and as a safety net for any
            // future manual insert).
            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "approved");

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ApprovalStatus",
                table: "Users",
                column: "ApprovalStatus");

            // ── CompanyDetails: fields the draft DTO accepted but the
            // entity used to drop/derive — see CompanyDetailsService. ────
            migrationBuilder.AddColumn<int>(
                name: "YearsOfExperience",
                table: "CompanyDetails",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryBankName",
                table: "CompanyDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IbanNumber",
                table: "CompanyDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SwiftBicCode",
                table: "CompanyDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BankBranchNameCity",
                table: "CompanyDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            // ── Projects: dispute reason + resolution ───────────────────
            migrationBuilder.AddColumn<string>(
                name: "DisputeReason",
                table: "Projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DisputeRaisedAt",
                table: "Projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisputeResolutionMessage",
                table: "Projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DisputeResolvedAt",
                table: "Projects",
                type: "timestamp with time zone",
                nullable: true);

            // Single seeded admin account — one admin sees the whole back
            // office (whitelist queue, users, disputes), per the confirmed
            // scope for this pass. Same pattern as the seeded bank
            // account. Password hash is bcrypt for "TrovaAdmin#2026";
            // change it (via change-password, once logged in) before this
            // goes anywhere near production.
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[]
                {
                    "Id", "Name", "Email", "PasswordHash", "Phone", "Role",
                    "IsBanned", "IsVerified", "EmailVerificationCode", "EmailVerificationCodeExpiry",
                    "PasswordResetToken", "PasswordResetTokenExpiry",
                    "IsIdentityVerified", "NationalId", "IdentityVerificationMethod",
                    "ApprovalStatus", "RejectionReason", "ApprovedAt",
                    "CreatedAt", "UpdatedAt"
                },
                values: new object[]
                {
                    AdminUserId, "Trova Admin", "admin@trova.jo",
                    "$2b$11$NHltXXO3nVsKiF33xkQR7uLPmamLp.xtsldByidImfH1o1pOXAdW.",
                    null, "admin",
                    false, true, null, null,
                    null, null,
                    false, null, null,
                    "approved", null, new DateTime(2026, 7, 23, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 7, 23, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 7, 23, 0, 0, 0, DateTimeKind.Utc)
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: AdminUserId);

            migrationBuilder.DropColumn(
                name: "DisputeReason",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "DisputeRaisedAt",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "DisputeResolutionMessage",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "DisputeResolvedAt",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "YearsOfExperience",
                table: "CompanyDetails");

            migrationBuilder.DropColumn(
                name: "PrimaryBankName",
                table: "CompanyDetails");

            migrationBuilder.DropColumn(
                name: "IbanNumber",
                table: "CompanyDetails");

            migrationBuilder.DropColumn(
                name: "SwiftBicCode",
                table: "CompanyDetails");

            migrationBuilder.DropColumn(
                name: "BankBranchNameCity",
                table: "CompanyDetails");

            migrationBuilder.DropIndex(
                name: "IX_Users_ApprovalStatus",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "Users");
        }
    }
}
