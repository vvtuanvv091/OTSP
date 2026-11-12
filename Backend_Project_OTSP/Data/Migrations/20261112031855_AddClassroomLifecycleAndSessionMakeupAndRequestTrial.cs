using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend_Project_OTSP.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClassroomLifecycleAndSessionMakeupAndRequestTrial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DurationWeeks",
                table: "ConnectionRequests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsTrial",
                table: "ConnectionRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "RequestedEndDate",
                table: "ConnectionRequests",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "RequestedStartDate",
                table: "ConnectionRequests",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DurationHours",
                table: "ClassSessions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMakeup",
                table: "ClassSessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EndDate",
                table: "Classrooms",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "RegistrationDeadline",
                table: "Classrooms",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalSessions",
                table: "Classrooms",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationWeeks",
                table: "ConnectionRequests");

            migrationBuilder.DropColumn(
                name: "IsTrial",
                table: "ConnectionRequests");

            migrationBuilder.DropColumn(
                name: "RequestedEndDate",
                table: "ConnectionRequests");

            migrationBuilder.DropColumn(
                name: "RequestedStartDate",
                table: "ConnectionRequests");

            migrationBuilder.DropColumn(
                name: "DurationHours",
                table: "ClassSessions");

            migrationBuilder.DropColumn(
                name: "IsMakeup",
                table: "ClassSessions");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "Classrooms");

            migrationBuilder.DropColumn(
                name: "RegistrationDeadline",
                table: "Classrooms");

            migrationBuilder.DropColumn(
                name: "TotalSessions",
                table: "Classrooms");
        }
    }
}
