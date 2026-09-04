using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Projects.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemDeliverablesAndHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Observations",
                schema: "projects",
                table: "work_items",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deliverables_json",
                schema: "projects",
                table: "work_items",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "work_item_deliverables",
                schema: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TypeId = table.Column<int>(type: "integer", nullable: false),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_item_deliverables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "work_item_histories",
                schema: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Field = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FromJson = table.Column<string>(type: "jsonb", nullable: true),
                    ToJson = table.Column<string>(type: "jsonb", nullable: true),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_item_histories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_work_item_deliverables_WorkItemId",
                schema: "projects",
                table: "work_item_deliverables",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_work_item_histories_TenantId",
                schema: "projects",
                table: "work_item_histories",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_work_item_histories_WorkItemId_CreatedAt",
                schema: "projects",
                table: "work_item_histories",
                columns: new[] { "WorkItemId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "work_item_deliverables",
                schema: "projects");

            migrationBuilder.DropTable(
                name: "work_item_histories",
                schema: "projects");

            migrationBuilder.DropColumn(
                name: "Observations",
                schema: "projects",
                table: "work_items");

            migrationBuilder.DropColumn(
                name: "deliverables_json",
                schema: "projects",
                table: "work_items");
        }
    }
}
