using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kronxy.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260922190000_AddJobActiveExecutionLease")]
public partial class AddJobActiveExecutionLease : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "active_execution_started_on_utc",
            table: "jobs",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "last_active_progress_on_utc",
            table: "jobs",
            type: "timestamp with time zone",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "active_execution_started_on_utc",
            table: "jobs");

        migrationBuilder.DropColumn(
            name: "last_active_progress_on_utc",
            table: "jobs");
    }
}
