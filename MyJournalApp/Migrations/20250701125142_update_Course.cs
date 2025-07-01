using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyJournalApp.Migrations
{
    /// <inheritdoc />
    public partial class update_Course : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "Courses");

            migrationBuilder.RenameColumn(
                name: "GradeIds",
                table: "Courses",
                newName: "GroupsID");

            migrationBuilder.AddColumn<Guid>(
                name: "TeacherId",
                table: "Courses",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TeacherId",
                table: "Courses");

            migrationBuilder.RenameColumn(
                name: "GroupsID",
                table: "Courses",
                newName: "GradeIds");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Courses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
