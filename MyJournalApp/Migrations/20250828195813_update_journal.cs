using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyJournalApp.Migrations
{
    /// <inheritdoc />
    public partial class update_journal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "JournalEntries",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "JournalEntries");
        }
    }
}
