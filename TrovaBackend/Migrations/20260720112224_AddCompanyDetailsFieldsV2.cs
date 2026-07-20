using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrovaBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyDetailsFieldsV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "YearsInOperation",
                table: "CompanyDetails",
                newName: "YearOfEstablishment");

            migrationBuilder.RenameColumn(
                name: "CompanyName",
                table: "CompanyDetails",
                newName: "TradingName");

            migrationBuilder.AddColumn<string>(
                name: "BusinessLicenseNumber",
                table: "CompanyDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContractorClassificationGrade",
                table: "CompanyDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CountryOfRegistration",
                table: "CompanyDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LegalCompanyName",
                table: "CompanyDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LegalStructure",
                table: "CompanyDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PositionTitle",
                table: "CompanyDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PrimaryContactName",
                table: "CompanyDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PrimaryEmail",
                table: "CompanyDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PrimaryPhoneNumber",
                table: "CompanyDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RegisteredAddress",
                table: "CompanyDetails",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TaxVatNumber",
                table: "CompanyDetails",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BusinessLicenseNumber",
                table: "CompanyDetails");

            migrationBuilder.DropColumn(
                name: "ContractorClassificationGrade",
                table: "CompanyDetails");

            migrationBuilder.DropColumn(
                name: "CountryOfRegistration",
                table: "CompanyDetails");

            migrationBuilder.DropColumn(
                name: "LegalCompanyName",
                table: "CompanyDetails");

            migrationBuilder.DropColumn(
                name: "LegalStructure",
                table: "CompanyDetails");

            migrationBuilder.DropColumn(
                name: "PositionTitle",
                table: "CompanyDetails");

            migrationBuilder.DropColumn(
                name: "PrimaryContactName",
                table: "CompanyDetails");

            migrationBuilder.DropColumn(
                name: "PrimaryEmail",
                table: "CompanyDetails");

            migrationBuilder.DropColumn(
                name: "PrimaryPhoneNumber",
                table: "CompanyDetails");

            migrationBuilder.DropColumn(
                name: "RegisteredAddress",
                table: "CompanyDetails");

            migrationBuilder.DropColumn(
                name: "TaxVatNumber",
                table: "CompanyDetails");

            migrationBuilder.RenameColumn(
                name: "YearOfEstablishment",
                table: "CompanyDetails",
                newName: "YearsInOperation");

            migrationBuilder.RenameColumn(
                name: "TradingName",
                table: "CompanyDetails",
                newName: "CompanyName");
        }
    }
}
