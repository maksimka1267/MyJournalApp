using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyJournalApp.Migrations
{
    /// <inheritdoc />
    public partial class update_journal_max_value : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxValue",
                table: "JournalEntries",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxValue",
                table: "JournalEntries");
        }
    }
}
