using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiProcessing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AI_Processing_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ai_processing");

            migrationBuilder.CreateTable(
                name: "chunk_references",
                schema: "ai_processing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChunkId = table.Column<int>(type: "integer", nullable: false),
                    Classification = table.Column<string>(type: "text", nullable: false),
                    IsSafe = table.Column<bool>(type: "boolean", nullable: false),
                    IsCurrentVersion = table.Column<bool>(type: "boolean", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chunk_references", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "llm_operations",
                schema: "ai_processing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationTypeId = table.Column<int>(type: "integer", nullable: false),
                    OperationStatusId = table.Column<int>(type: "integer", nullable: false),
                    ModelProvider = table.Column<string>(type: "text", nullable: false),
                    ModelName = table.Column<string>(type: "text", nullable: false),
                    ModelVersion = table.Column<string>(type: "text", nullable: false),
                    PromptVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageStatusesJson = table.Column<string>(type: "jsonb", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    LastErrorStage = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_llm_operations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "llm_prompt_versions",
                schema: "ai_processing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationTypeId = table.Column<int>(type: "integer", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Template = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_llm_prompt_versions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "llm_results",
                schema: "ai_processing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationTypeId = table.Column<int>(type: "integer", nullable: false),
                    ProvenanceJson = table.Column<string>(type: "jsonb", nullable: false),
                    Content = table.Column<string>(type: "character varying(50000)", maxLength: 50000, nullable: false),
                    ProposedValueJson = table.Column<string>(type: "text", nullable: true),
                    ChunkReferencesJson = table.Column<string>(type: "text", nullable: true),
                    ReviewStatusId = table.Column<int>(type: "integer", nullable: false),
                    QualityIndicatorJson = table.Column<string>(type: "text", nullable: true),
                    SupersededBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_llm_results", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "llm_reviews",
                schema: "ai_processing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<int>(type: "integer", nullable: false),
                    Rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_llm_reviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "ai_processing",
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
                name: "review_policies",
                schema: "ai_processing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationTypeId = table.Column<int>(type: "integer", nullable: false),
                    Classification = table.Column<string>(type: "text", nullable: false),
                    RequiresReview = table.Column<bool>(type: "boolean", nullable: false),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_policies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_chunk_references_TenantId_Classification",
                schema: "ai_processing",
                table: "chunk_references",
                columns: new[] { "TenantId", "Classification" });

            migrationBuilder.CreateIndex(
                name: "IX_chunk_references_TenantId_DocumentId",
                schema: "ai_processing",
                table: "chunk_references",
                columns: new[] { "TenantId", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_llm_operations_TenantId_DocumentId",
                schema: "ai_processing",
                table: "llm_operations",
                columns: new[] { "TenantId", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_llm_operations_TenantId_OperationStatusId",
                schema: "ai_processing",
                table: "llm_operations",
                columns: new[] { "TenantId", "OperationStatusId" });

            migrationBuilder.CreateIndex(
                name: "IX_llm_prompt_versions_OperationTypeId_IsPublished",
                schema: "ai_processing",
                table: "llm_prompt_versions",
                columns: new[] { "OperationTypeId", "IsPublished" });

            migrationBuilder.CreateIndex(
                name: "IX_llm_prompt_versions_OperationTypeId_VersionNumber",
                schema: "ai_processing",
                table: "llm_prompt_versions",
                columns: new[] { "OperationTypeId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_llm_results_OperationId",
                schema: "ai_processing",
                table: "llm_results",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_llm_results_TenantId_DocumentId",
                schema: "ai_processing",
                table: "llm_results",
                columns: new[] { "TenantId", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_llm_results_TenantId_ReviewStatusId",
                schema: "ai_processing",
                table: "llm_results",
                columns: new[] { "TenantId", "ReviewStatusId" });

            migrationBuilder.CreateIndex(
                name: "IX_llm_reviews_ResultId",
                schema: "ai_processing",
                table: "llm_reviews",
                column: "ResultId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedOn",
                schema: "ai_processing",
                table: "OutboxMessages",
                column: "ProcessedOn");

            migrationBuilder.CreateIndex(
                name: "IX_review_policies_TenantId_OperationTypeId_Classification",
                schema: "ai_processing",
                table: "review_policies",
                columns: new[] { "TenantId", "OperationTypeId", "Classification" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chunk_references",
                schema: "ai_processing");

            migrationBuilder.DropTable(
                name: "llm_operations",
                schema: "ai_processing");

            migrationBuilder.DropTable(
                name: "llm_prompt_versions",
                schema: "ai_processing");

            migrationBuilder.DropTable(
                name: "llm_results",
                schema: "ai_processing");

            migrationBuilder.DropTable(
                name: "llm_reviews",
                schema: "ai_processing");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "ai_processing");

            migrationBuilder.DropTable(
                name: "review_policies",
                schema: "ai_processing");
        }
    }
}
