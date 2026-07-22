using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrovaBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddBidNoteAndProjectSubmittedDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedDate",
                table: "Projects",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "Bids",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubmittedDate",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "Bids");
        }
    }
}
