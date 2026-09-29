using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AITestHub.Migrations
{
    /// <inheritdoc />
    public partial class UpgradeTestCaseManagementV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExecutionNotes",
                table: "test_cases",
                type: "character varying(3000)",
                maxLength: 3000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastExecutedAt",
                table: "test_cases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Preconditions",
                table: "test_cases",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "test_cases",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "test_cases",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Steps",
                table: "test_cases",
                type: "character varying(5000)",
                maxLength: 5000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TestType",
                table: "test_cases",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExecutionNotes",
                table: "test_cases");

            migrationBuilder.DropColumn(
                name: "LastExecutedAt",
                table: "test_cases");

            migrationBuilder.DropColumn(
                name: "Preconditions",
                table: "test_cases");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "test_cases");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "test_cases");

            migrationBuilder.DropColumn(
                name: "Steps",
                table: "test_cases");

            migrationBuilder.DropColumn(
                name: "TestType",
                table: "test_cases");
        }
    }
}
