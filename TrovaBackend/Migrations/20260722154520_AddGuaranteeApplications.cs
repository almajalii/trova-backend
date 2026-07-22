using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrovaBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddGuaranteeApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PublicCode",
                table: "CompanyDetails",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GuaranteeApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationCode = table.Column<string>(type: "text", nullable: false),
                    ContractorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    BidId = table.Column<Guid>(type: "uuid", nullable: false),
                    BeneficiaryId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuaranteeType = table.Column<string>(type: "text", nullable: false),
                    GuaranteedAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    ValidityStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidityExpiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SpecialConditions = table.Column<string>(type: "text", nullable: true),
                    ConfirmAccurate = table.Column<bool>(type: "boolean", nullable: false),
                    AgreeIndemnify = table.Column<bool>(type: "boolean", nullable: false),
                    AcceptTerms = table.Column<bool>(type: "boolean", nullable: false),
                    SignatureName = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuaranteeApplications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GuaranteeDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GuaranteeApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<string>(type: "text", nullable: false),
                    OriginalFileName = table.Column<string>(type: "text", nullable: false),
                    StoredFileName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuaranteeDocuments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyDetails_PublicCode",
                table: "CompanyDetails",
                column: "PublicCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuaranteeApplications_ApplicationCode",
                table: "GuaranteeApplications",
                column: "ApplicationCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuaranteeApplications_ContractorId",
                table: "GuaranteeApplications",
                column: "ContractorId");

            migrationBuilder.CreateIndex(
                name: "IX_GuaranteeApplications_ProjectId",
                table: "GuaranteeApplications",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_GuaranteeDocuments_GuaranteeApplicationId",
                table: "GuaranteeDocuments",
                column: "GuaranteeApplicationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuaranteeApplications");

            migrationBuilder.DropTable(
                name: "GuaranteeDocuments");

            migrationBuilder.DropIndex(
                name: "IX_CompanyDetails_PublicCode",
                table: "CompanyDetails");

            migrationBuilder.DropColumn(
                name: "PublicCode",
                table: "CompanyDetails");
        }
    }
}
