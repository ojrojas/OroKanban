using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Documents.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "documents");

            migrationBuilder.CreateTable(
                name: "classification_rules",
                schema: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    RuleSetJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_classification_rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "document_access_entries",
                schema: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Granted = table.Column<bool>(type: "boolean", nullable: false),
                    ClassificationValue = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RuleVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_access_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "document_explicit_grants",
                schema: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    GranteeUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_explicit_grants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "document_processing_jobs",
                schema: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentStageId = table.Column<int>(type: "integer", nullable: false),
                    OverallStatus = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    LastErrorStageId = table.Column<int>(type: "integer", nullable: true),
                    RuleVersion = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_processing_jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "document_versions",
                schema: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MetadataSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    MetadataEffectiveDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MetadataExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsSafe = table.Column<bool>(type: "boolean", nullable: false),
                    ScanStatusId = table.Column<int>(type: "integer", nullable: false),
                    ScannedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ScannedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_versions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                schema: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ClassificationValue = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ClassificationLevelId = table.Column<int>(type: "integer", nullable: false),
                    RuleVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CurrentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MimeType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProvenanceSource = table.Column<string>(type: "text", nullable: false),
                    OriginalFilename = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RetentionRetainUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RetentionDays = table.Column<int>(type: "integer", nullable: true),
                    RetentionLegalHold = table.Column<bool>(type: "boolean", nullable: false),
                    IsSafe = table.Column<bool>(type: "boolean", nullable: false),
                    ScanStatusId = table.Column<int>(type: "integer", nullable: false),
                    ScannedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ScannedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "documents",
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

            migrationBuilder.CreateIndex(
                name: "IX_classification_rules_OrganizationId_Version",
                schema: "documents",
                table: "classification_rules",
                columns: new[] { "OrganizationId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_access_entries_DocumentId_TenantId",
                schema: "documents",
                table: "document_access_entries",
                columns: new[] { "DocumentId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_document_access_entries_Timestamp",
                schema: "documents",
                table: "document_access_entries",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_document_explicit_grants_DocumentId_GranteeUserId",
                schema: "documents",
                table: "document_explicit_grants",
                columns: new[] { "DocumentId", "GranteeUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_processing_jobs_DocumentId",
                schema: "documents",
                table: "document_processing_jobs",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_document_versions_DocumentId_VersionNumber",
                schema: "documents",
                table: "document_versions",
                columns: new[] { "DocumentId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_versions_IsSafe",
                schema: "documents",
                table: "document_versions",
                column: "IsSafe");

            migrationBuilder.CreateIndex(
                name: "IX_documents_ContentHash",
                schema: "documents",
                table: "documents",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_documents_TenantId_OwnerId",
                schema: "documents",
                table: "documents",
                columns: new[] { "TenantId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_documents_TenantId_ProjectId",
                schema: "documents",
                table: "documents",
                columns: new[] { "TenantId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedOn",
                schema: "documents",
                table: "OutboxMessages",
                column: "ProcessedOn");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "classification_rules",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "document_access_entries",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "document_explicit_grants",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "document_processing_jobs",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "document_versions",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "documents",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "documents");
        }
    }
}
