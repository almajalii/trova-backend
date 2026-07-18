using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrovaBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanyDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyName = table.Column<string>(type: "text", nullable: false),
                    Sectors = table.Column<List<string>>(type: "text[]", nullable: false),
                    RegistrationNumber = table.Column<string>(type: "text", nullable: false),
                    YearsInOperation = table.Column<int>(type: "integer", nullable: false),
                    TeamSize = table.Column<int>(type: "integer", nullable: false),
                    AnnualRevenueJod = table.Column<decimal>(type: "numeric", nullable: false),
                    ClassificationCode = table.Column<string>(type: "text", nullable: false),
                    ClassificationLabel = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyDetails", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyDetails_UserId",
                table: "CompanyDetails",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyDetails");
        }
    }
}
