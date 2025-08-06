using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyJournalApp.Migrations
{
    /// <inheritdoc />
    public partial class update_schedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Lessons_Schedules_ScheduleId",
                table: "Lessons");

            migrationBuilder.DropIndex(
                name: "IX_Lessons_ScheduleId",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "ScheduleId",
                table: "Lessons");

            migrationBuilder.AddColumn<string>(
                name: "Lessons",
                table: "Schedules",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Lessons",
                table: "Schedules");

            migrationBuilder.AddColumn<Guid>(
                name: "ScheduleId",
                table: "Lessons",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lessons_ScheduleId",
                table: "Lessons",
                column: "ScheduleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Lessons_Schedules_ScheduleId",
                table: "Lessons",
                column: "ScheduleId",
                principalTable: "Schedules",
                principalColumn: "Id");
        }
    }
}
