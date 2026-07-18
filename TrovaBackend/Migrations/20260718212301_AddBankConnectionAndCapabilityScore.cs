using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrovaBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddBankConnectionAndCapabilityScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankCode = table.Column<string>(type: "text", nullable: false),
                    BankName = table.Column<string>(type: "text", nullable: false),
                    AccountAddress = table.Column<string>(type: "text", nullable: false),
                    AccountCurrency = table.Column<string>(type: "text", nullable: false),
                    AccountStatus = table.Column<string>(type: "text", nullable: false),
                    AvailableBalanceAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    NumberOfCurrentDebts = table.Column<int>(type: "integer", nullable: false),
                    AverageMonthlyCashflowChangePercent = table.Column<decimal>(type: "numeric", nullable: false),
                    RemainingDebtCapacityJod = table.Column<decimal>(type: "numeric", nullable: false),
                    NumberOfDelinquentDebts = table.Column<int>(type: "integer", nullable: false),
                    ConnectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CapabilityScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OverallScore = table.Column<int>(type: "integer", nullable: false),
                    TierLabel = table.Column<string>(type: "text", nullable: false),
                    TotalProjects = table.Column<int>(type: "integer", nullable: false),
                    FailedProjects = table.Column<int>(type: "integer", nullable: false),
                    AvgRating = table.Column<double>(type: "double precision", nullable: false),
                    NumberOfCurrentDebtsScore = table.Column<int>(type: "integer", nullable: false),
                    NumberOfCurrentDebtsDescription = table.Column<string>(type: "text", nullable: false),
                    DebtCapacityScore = table.Column<int>(type: "integer", nullable: false),
                    DebtCapacityDescription = table.Column<string>(type: "text", nullable: false),
                    CompanyAssetsValueScore = table.Column<int>(type: "integer", nullable: false),
                    CompanyAssetsValueDescription = table.Column<string>(type: "text", nullable: false),
                    DelinquentDebtsScore = table.Column<int>(type: "integer", nullable: false),
                    DelinquentDebtsDescription = table.Column<string>(type: "text", nullable: false),
                    PaymentHistoryScore = table.Column<int>(type: "integer", nullable: false),
                    PaymentHistoryDescription = table.Column<string>(type: "text", nullable: false),
                    CurrentWorkloadScore = table.Column<int>(type: "integer", nullable: false),
                    CurrentWorkloadDescription = table.Column<string>(type: "text", nullable: false),
                    ProjectDeliveryHistoryScore = table.Column<int>(type: "integer", nullable: false),
                    ProjectDeliveryHistoryDescription = table.Column<string>(type: "text", nullable: false),
                    CashflowTrendsScore = table.Column<int>(type: "integer", nullable: false),
                    CashflowTrendsDescription = table.Column<string>(type: "text", nullable: false),
                    LastCalculatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapabilityScores", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankConnections_UserId",
                table: "BankConnections",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CapabilityScores_UserId",
                table: "CapabilityScores",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankConnections");

            migrationBuilder.DropTable(
                name: "CapabilityScores");
        }
    }
}
