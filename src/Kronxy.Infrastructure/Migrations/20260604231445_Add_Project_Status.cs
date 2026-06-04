using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kronxy.Infrastructure.Migrations
{
    public partial class Add_Project_Status : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "project_status_id",
                table: "projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE projects
                SET project_status_id = CASE status
                    WHEN 1 THEN 'df1eaebd-23b8-4426-a852-f5e5a3b4f22c'::uuid
                    WHEN 2 THEN '96a5e296-7ff1-4244-bf7b-06a1bb6f5690'::uuid
                    WHEN 3 THEN '92e5a31d-1f18-4fc3-aa2c-39fae7ea0928'::uuid
                    WHEN 4 THEN '57fa4d39-c3dc-48e1-8614-c485341393ed'::uuid
                    WHEN 5 THEN '22a81991-1d69-4b08-b80b-0d21d04a5595'::uuid
                    ELSE 'df1eaebd-23b8-4426-a852-f5e5a3b4f22c'::uuid
                END
                WHERE project_status_id IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "project_status_id",
                table: "projects",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_projects_project_status_id",
                table: "projects",
                column: "project_status_id");

            migrationBuilder.AddForeignKey(
                name: "fk_projects_catalog_items_project_status_id",
                table: "projects",
                column: "project_status_id",
                principalTable: "catalog_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropColumn(
                name: "status",
                table: "projects");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "status",
                table: "projects",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE projects
                SET status = CASE project_status_id
                    WHEN 'df1eaebd-23b8-4426-a852-f5e5a3b4f22c'::uuid THEN 1
                    WHEN '96a5e296-7ff1-4244-bf7b-06a1bb6f5690'::uuid THEN 2
                    WHEN '92e5a31d-1f18-4fc3-aa2c-39fae7ea0928'::uuid THEN 3
                    WHEN '57fa4d39-c3dc-48e1-8614-c485341393ed'::uuid THEN 4
                    WHEN '22a81991-1d69-4b08-b80b-0d21d04a5595'::uuid THEN 5
                    ELSE 1
                END
                WHERE status IS NULL;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "status",
                table: "projects",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "fk_projects_catalog_items_project_status_id",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "ix_projects_project_status_id",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "project_status_id",
                table: "projects");
        }
    }
}