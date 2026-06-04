using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kronxy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Project_Type : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "project_type_id",
                table: "projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
        UPDATE projects
        SET project_type_id = '3654b5ba-14fb-48d9-9f0a-8f140a0a13ec'
        WHERE project_type_id IS NULL;
        """);

            migrationBuilder.AlterColumn<Guid>(
                name: "project_type_id",
                table: "projects",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_projects_project_type_id",
                table: "projects",
                column: "project_type_id");

            migrationBuilder.AddForeignKey(
                name: "fk_projects_catalog_items_project_type_id",
                table: "projects",
                column: "project_type_id",
                principalTable: "catalog_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_projects_catalog_items_project_type_id",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "ix_projects_project_type_id",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "project_type_id",
                table: "projects");
        }
    }
}
