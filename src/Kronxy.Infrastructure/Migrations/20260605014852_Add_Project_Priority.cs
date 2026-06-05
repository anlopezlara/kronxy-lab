using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kronxy.Infrastructure.Migrations
{
    public partial class Add_Project_Priority : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "project_priority_id",
                table: "projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE projects
                SET project_priority_id = CASE priority
                    WHEN 1 THEN '5f696430-cabd-48e1-ab88-fdf8e23a3c0e'::uuid
                    WHEN 2 THEN '442e7610-8e68-424e-b7a3-b69607136639'::uuid
                    WHEN 3 THEN '3b9b2293-8824-46be-8ce9-85a90e5b4e38'::uuid
                    WHEN 4 THEN '0112bfac-fd97-40e0-b897-93635fc0dbc4'::uuid
                    ELSE '442e7610-8e68-424e-b7a3-b69607136639'::uuid
                END
                WHERE project_priority_id IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "project_priority_id",
                table: "projects",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_projects_project_priority_id",
                table: "projects",
                column: "project_priority_id");

            migrationBuilder.AddForeignKey(
                name: "fk_projects_catalog_items_project_priority_id",
                table: "projects",
                column: "project_priority_id",
                principalTable: "catalog_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropColumn(
                name: "priority",
                table: "projects");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "priority",
                table: "projects",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE projects
                SET priority = CASE project_priority_id
                    WHEN '5f696430-cabd-48e1-ab88-fdf8e23a3c0e'::uuid THEN 1
                    WHEN '442e7610-8e68-424e-b7a3-b69607136639'::uuid THEN 2
                    WHEN '3b9b2293-8824-46be-8ce9-85a90e5b4e38'::uuid THEN 3
                    WHEN '0112bfac-fd97-40e0-b897-93635fc0dbc4'::uuid THEN 4
                    ELSE 2
                END
                WHERE priority IS NULL;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "priority",
                table: "projects",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "fk_projects_catalog_items_project_priority_id",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "ix_projects_project_priority_id",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "project_priority_id",
                table: "projects");
        }
    }
}