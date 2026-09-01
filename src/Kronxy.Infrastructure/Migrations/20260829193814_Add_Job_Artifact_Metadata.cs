using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kronxy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Job_Artifact_Metadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "job_artifacts",
                columns: table => new
                {
                    artifact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    artifact_type = table.Column<int>(type: "integer", nullable: false),
                    relative_path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_artifacts", x => x.artifact_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_job_artifacts_job_id",
                table: "job_artifacts",
                column: "job_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_artifacts_job_id_run_id",
                table: "job_artifacts",
                columns: new[] { "job_id", "run_id" });

            migrationBuilder.CreateIndex(
                name: "ix_job_artifacts_job_run_type",
                table: "job_artifacts",
                columns: new[] { "job_id", "run_id", "artifact_type" });

            migrationBuilder.CreateIndex(
                name: "ux_job_artifacts_relative_path",
                table: "job_artifacts",
                column: "relative_path",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_artifacts");
        }
    }
}
