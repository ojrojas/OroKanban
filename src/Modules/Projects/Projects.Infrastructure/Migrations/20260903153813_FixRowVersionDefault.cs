using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Projects.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixRowVersionDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "projects");

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    OccurredOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                schema: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ManagerId = table.Column<Guid>(type: "uuid", nullable: false),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    PriorityId = table.Column<int>(type: "integer", nullable: false),
                    CriticalityId = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false, defaultValue: new byte[0])
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "work_item_dependencies",
                schema: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DependentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrincipalId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_item_dependencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "work_items",
                schema: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    TypeId = table.Column<int>(type: "integer", nullable: false),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    PriorityId = table.Column<int>(type: "integer", nullable: false),
                    CriticalityId = table.Column<int>(type: "integer", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResponsibleId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewerId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EstimatedHours = table.Column<decimal>(type: "numeric", nullable: false),
                    ActualHours = table.Column<decimal>(type: "numeric", nullable: false),
                    ProgressPercent = table.Column<int>(type: "integer", nullable: false),
                    tags_json = table.Column<string>(type: "jsonb", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false, defaultValue: new byte[0]),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "project_members",
                schema: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_members_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "projects",
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_milestones",
                schema: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsReached = table.Column<bool>(type: "boolean", nullable: false),
                    ReachedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_milestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_milestones_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "projects",
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedOn",
                schema: "projects",
                table: "OutboxMessages",
                column: "ProcessedOn");

            migrationBuilder.CreateIndex(
                name: "IX_project_members_ProjectId_UserId",
                schema: "projects",
                table: "project_members",
                columns: new[] { "ProjectId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_project_milestones_ProjectId",
                schema: "projects",
                table: "project_milestones",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_projects_TenantId_Name",
                schema: "projects",
                table: "projects",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_item_dependencies_DependentId",
                schema: "projects",
                table: "work_item_dependencies",
                column: "DependentId");

            migrationBuilder.CreateIndex(
                name: "IX_work_item_dependencies_DependentId_PrincipalId",
                schema: "projects",
                table: "work_item_dependencies",
                columns: new[] { "DependentId", "PrincipalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_item_dependencies_PrincipalId",
                schema: "projects",
                table: "work_item_dependencies",
                column: "PrincipalId");

            migrationBuilder.CreateIndex(
                name: "IX_work_items_ProjectId_ParentId",
                schema: "projects",
                table: "work_items",
                columns: new[] { "ProjectId", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_work_items_ResponsibleId",
                schema: "projects",
                table: "work_items",
                column: "ResponsibleId");

            migrationBuilder.CreateIndex(
                name: "IX_work_items_TenantId_ProjectId",
                schema: "projects",
                table: "work_items",
                columns: new[] { "TenantId", "ProjectId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "projects");

            migrationBuilder.DropTable(
                name: "project_members",
                schema: "projects");

            migrationBuilder.DropTable(
                name: "project_milestones",
                schema: "projects");

            migrationBuilder.DropTable(
                name: "work_item_dependencies",
                schema: "projects");

            migrationBuilder.DropTable(
                name: "work_items",
                schema: "projects");

            migrationBuilder.DropTable(
                name: "projects",
                schema: "projects");
        }
    }
}
