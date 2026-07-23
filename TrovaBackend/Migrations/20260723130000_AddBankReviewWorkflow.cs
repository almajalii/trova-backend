using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrovaBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddBankReviewWorkflow : Migration
    {
        // Fixed so this migration is idempotent/reproducible across
        // environments — same convention as any other seeded row would
        // need, just none existed before this one.
        private static readonly Guid BankUserId = new Guid("bcfa9019-8819-4c89-824c-63e1b1acb59b");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "IssuedAt",
                table: "GuaranteeApplications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectedBy",
                table: "GuaranteeApplications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "GuaranteeApplications",
                type: "text",
                nullable: true);

            // Single seeded bank account — one bank user sees every
            // guarantee application, per the confirmed scope for this
            // pass. Password hash is bcrypt for "TrovaBank#2026"; change
            // it (via change-password, once logged in) before this goes
            // anywhere near production.
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[]
                {
                    "Id", "Name", "Email", "PasswordHash", "Phone", "Role",
                    "IsBanned", "IsVerified", "EmailVerificationCode", "EmailVerificationCodeExpiry",
                    "PasswordResetToken", "PasswordResetTokenExpiry",
                    "IsIdentityVerified", "NationalId", "IdentityVerificationMethod",
                    "CreatedAt", "UpdatedAt"
                },
                values: new object[]
                {
                    BankUserId, "Trova Bank Officer", "bank@trova.jo",
                    "$2b$11$I8LqkBJ9IM739z9LqwJqhe70wAcqFfL.TT5ZmYaI2OcHZT9L3R1pe",
                    null, "bank",
                    false, true, null, null,
                    null, null,
                    false, null, null,
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
                keyValue: BankUserId);

            migrationBuilder.DropColumn(
                name: "IssuedAt",
                table: "GuaranteeApplications");

            migrationBuilder.DropColumn(
                name: "RejectedBy",
                table: "GuaranteeApplications");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "GuaranteeApplications");
        }
    }
}
