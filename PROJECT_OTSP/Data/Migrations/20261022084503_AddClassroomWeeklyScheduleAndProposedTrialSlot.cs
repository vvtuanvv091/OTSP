using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PROJECT_OTSP.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClassroomWeeklyScheduleAndProposedTrialSlot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ProposedTrialSlotUtc",
                table: "ConnectionRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "HoursPerSession",
                table: "Classrooms",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "SessionsPerWeek",
                table: "Classrooms",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ClassroomWeeklySchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClassroomId = table.Column<int>(type: "integer", nullable: false),
                    SessionIndex = table.Column<int>(type: "integer", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassroomWeeklySchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassroomWeeklySchedules_Classrooms_ClassroomId",
                        column: x => x.ClassroomId,
                        principalTable: "Classrooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClassroomWeeklySchedules_ClassroomId_DayOfWeek_StartTime",
                table: "ClassroomWeeklySchedules",
                columns: new[] { "ClassroomId", "DayOfWeek", "StartTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClassroomWeeklySchedules");

            migrationBuilder.DropColumn(
                name: "ProposedTrialSlotUtc",
                table: "ConnectionRequests");

            migrationBuilder.DropColumn(
                name: "HoursPerSession",
                table: "Classrooms");

            migrationBuilder.DropColumn(
                name: "SessionsPerWeek",
                table: "Classrooms");
        }
    }
}
