using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PROJECT_OTSP.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClassroomCoverImagePublicId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoverImagePublicId",
                table: "Classrooms",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoverImagePublicId",
                table: "Classrooms");
        }
    }
}
