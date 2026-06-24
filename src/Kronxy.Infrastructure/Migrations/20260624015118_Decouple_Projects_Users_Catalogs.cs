using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kronxy.Infrastructure.Migrations
{
    public partial class Decouple_Projects_Users_Catalogs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey("fk_projects_catalog_items_project_priority_id", "projects");
            migrationBuilder.DropForeignKey("fk_projects_catalog_items_project_status_id", "projects");
            migrationBuilder.DropForeignKey("fk_projects_catalog_items_project_type_id", "projects");
            migrationBuilder.DropForeignKey("fk_users_catalog_items_role_id", "users");

            migrationBuilder.CreateTable(
                name: "project_priorities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table => table.PrimaryKey("pk_project_priorities", x => x.id));

            migrationBuilder.CreateTable(
                name: "project_statuses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table => table.PrimaryKey("pk_project_statuses", x => x.id));

            migrationBuilder.CreateTable(
                name: "project_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table => table.PrimaryKey("pk_project_types", x => x.id));

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table => table.PrimaryKey("pk_user_roles", x => x.id));

            migrationBuilder.Sql("""
                INSERT INTO project_types
                (id, code, name, description, display_order, is_active, created_on_utc, updated_on_utc, deleted_on_utc)
                SELECT ci.id, ci.code, ci.name, ci.description, ci.sort_order, ci.is_active, ci.created_on_utc, ci.updated_on_utc, ci.deleted_on_utc
                FROM catalog_items ci
                INNER JOIN catalogs c ON c.id = ci.catalog_id
                WHERE c.code = 'PROJECT_TYPE';
                """);

            migrationBuilder.Sql("""
                INSERT INTO project_statuses
                (id, code, name, description, display_order, is_active, created_on_utc, updated_on_utc, deleted_on_utc)
                SELECT ci.id, ci.code, ci.name, ci.description, ci.sort_order, ci.is_active, ci.created_on_utc, ci.updated_on_utc, ci.deleted_on_utc
                FROM catalog_items ci
                INNER JOIN catalogs c ON c.id = ci.catalog_id
                WHERE c.code = 'PROJECT_STATUS';
                """);

            migrationBuilder.Sql("""
                INSERT INTO project_priorities
                (id, code, name, description, display_order, is_active, created_on_utc, updated_on_utc, deleted_on_utc)
                SELECT ci.id, ci.code, ci.name, ci.description, ci.sort_order, ci.is_active, ci.created_on_utc, ci.updated_on_utc, ci.deleted_on_utc
                FROM catalog_items ci
                INNER JOIN catalogs c ON c.id = ci.catalog_id
                WHERE c.code = 'PROJECT_PRIORITY';
                """);

            migrationBuilder.Sql("""
                INSERT INTO user_roles
                (id, code, name, description, display_order, is_active, created_on_utc, updated_on_utc, deleted_on_utc)
                SELECT ci.id, ci.code, ci.name, ci.description, ci.sort_order, ci.is_active, ci.created_on_utc, ci.updated_on_utc, ci.deleted_on_utc
                FROM catalog_items ci
                INNER JOIN catalogs c ON c.id = ci.catalog_id
                WHERE c.code = 'USER_ROLE';
                """);

            migrationBuilder.CreateIndex("ix_project_priorities_code", "project_priorities", "code", unique: true);
            migrationBuilder.CreateIndex("ix_project_priorities_display_order", "project_priorities", "display_order");
            migrationBuilder.CreateIndex("ix_project_statuses_code", "project_statuses", "code", unique: true);
            migrationBuilder.CreateIndex("ix_project_statuses_display_order", "project_statuses", "display_order");
            migrationBuilder.CreateIndex("ix_project_types_code", "project_types", "code", unique: true);
            migrationBuilder.CreateIndex("ix_project_types_display_order", "project_types", "display_order");
            migrationBuilder.CreateIndex("ix_user_roles_code", "user_roles", "code", unique: true);
            migrationBuilder.CreateIndex("ix_user_roles_display_order", "user_roles", "display_order");

            migrationBuilder.AddForeignKey("fk_projects_project_priorities_project_priority_id", "projects", "project_priority_id", "project_priorities", principalColumn: "id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey("fk_projects_project_statuses_project_status_id", "projects", "project_status_id", "project_statuses", principalColumn: "id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey("fk_projects_project_types_project_type_id", "projects", "project_type_id", "project_types", principalColumn: "id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey("fk_users_user_roles_role_id", "users", "role_id", "user_roles", principalColumn: "id", onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey("fk_projects_project_priorities_project_priority_id", "projects");
            migrationBuilder.DropForeignKey("fk_projects_project_statuses_project_status_id", "projects");
            migrationBuilder.DropForeignKey("fk_projects_project_types_project_type_id", "projects");
            migrationBuilder.DropForeignKey("fk_users_user_roles_role_id", "users");

            migrationBuilder.DropTable("project_priorities");
            migrationBuilder.DropTable("project_statuses");
            migrationBuilder.DropTable("project_types");
            migrationBuilder.DropTable("user_roles");

            migrationBuilder.AddForeignKey("fk_projects_catalog_items_project_priority_id", "projects", "project_priority_id", "catalog_items", principalColumn: "id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey("fk_projects_catalog_items_project_status_id", "projects", "project_status_id", "catalog_items", principalColumn: "id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey("fk_projects_catalog_items_project_type_id", "projects", "project_type_id", "catalog_items", principalColumn: "id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey("fk_users_catalog_items_role_id", "users", "role_id", "catalog_items", principalColumn: "id", onDelete: ReferentialAction.Restrict);
        }
    }
}