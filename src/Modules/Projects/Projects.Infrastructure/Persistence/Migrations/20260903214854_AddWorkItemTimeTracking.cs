using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Projects.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemTimeTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "reopened_count",
                schema: "projects",
                table: "work_items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "started_at",
                schema: "projects",
                table: "work_items",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "reopened_count",
                schema: "projects",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "started_at",
                schema: "projects",
                table: "work_items");
        }
    }
}
