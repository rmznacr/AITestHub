using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AITestHub.Migrations
{
    /// <inheritdoc />
    public partial class AddExpectedResultToTestCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExpectedResult",
                table: "test_cases",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpectedResult",
                table: "test_cases");
        }
    }
}
