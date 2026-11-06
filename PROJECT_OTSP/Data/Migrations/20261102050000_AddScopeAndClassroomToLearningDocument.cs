using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PROJECT_OTSP.Data;

#nullable disable

namespace PROJECT_OTSP.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261102050000_AddScopeAndClassroomToLearningDocument")]
    public partial class AddScopeAndClassroomToLearningDocument : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN 
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LearningDocuments' AND column_name = 'Scope') THEN
                        ALTER TABLE ""LearningDocuments"" ADD COLUMN ""Scope"" integer NOT NULL DEFAULT 0;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LearningDocuments' AND column_name = 'ClassroomId') THEN
                        ALTER TABLE ""LearningDocuments"" ADD COLUMN ""ClassroomId"" integer NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LearningDocuments' AND column_name = 'Status') THEN
                        ALTER TABLE ""LearningDocuments"" ADD COLUMN ""Status"" integer NOT NULL DEFAULT 1;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LearningDocuments_Classrooms_ClassroomId') THEN
                        ALTER TABLE ""LearningDocuments"" ADD CONSTRAINT ""FK_LearningDocuments_Classrooms_ClassroomId"" 
                            FOREIGN KEY (""ClassroomId"") REFERENCES ""Classrooms"" (""Id"") ON DELETE SET NULL;
                    END IF;
                END $$;
                CREATE INDEX IF NOT EXISTS ""IX_LearningDocuments_ClassroomId"" ON ""LearningDocuments"" (""ClassroomId"");
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN 
                    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LearningDocuments_Classrooms_ClassroomId') THEN
                        ALTER TABLE ""LearningDocuments"" DROP CONSTRAINT ""FK_LearningDocuments_Classrooms_ClassroomId"";
                    END IF;
                END $$;
                DROP INDEX IF EXISTS ""IX_LearningDocuments_ClassroomId"";
            ");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "LearningDocuments");

            migrationBuilder.DropColumn(
                name: "ClassroomId",
                table: "LearningDocuments");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "LearningDocuments");
        }
    }
}

