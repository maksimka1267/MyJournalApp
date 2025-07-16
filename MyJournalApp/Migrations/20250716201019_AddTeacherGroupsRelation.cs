using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyJournalApp.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherGroupsRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubjectIds",
                table: "Teachers");

            migrationBuilder.AddColumn<string>(
                name: "GroupIds",
                table: "Teachers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GroupIds",
                table: "Teachers");

            migrationBuilder.AddColumn<string>(
                name: "SubjectIds",
                table: "Teachers",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
