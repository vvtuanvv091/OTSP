using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PROJECT_OTSP.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHourlyFeeToTutorSubject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "HourlyFee",
                table: "TutorSubjects",
                type: "numeric(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HourlyFee",
                table: "TutorSubjects");
        }
    }
}
