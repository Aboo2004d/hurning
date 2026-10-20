using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HiringPs.Core.Migrations
{
    /// <inheritdoc />
    public partial class PlatformImprovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConsecutiveFailures",
                table: "Sources",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "HealthWarning",
                table: "Sources",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "HealthWarningAt",
                table: "Sources",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastVerifiedAt",
                table: "SourceItems",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                table: "SocialPublications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Error",
                table: "SocialPublications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAt",
                table: "SocialPublications",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMergeTarget",
                table: "Opportunities",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedReference",
                table: "Opportunities",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SortDate",
                table: "Opportunities",
                type: "datetimeoffset",
                nullable: false,
                computedColumnSql: "COALESCE([SourcePublishedAt], [CreatedAt])",
                stored: true);

            migrationBuilder.CreateTable(
                name: "DuplicateCandidates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OpportunityId = table.Column<long>(type: "bigint", nullable: false),
                    CandidateId = table.Column<long>(type: "bigint", nullable: false),
                    Score = table.Column<double>(type: "float", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ResolvedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    MergeOperationId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DuplicateCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DuplicateCandidates_Opportunities_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "Opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DuplicateCandidates_Opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalTable: "Opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IndexNowEntries",
                columns: table => new
                {
                    OpportunityId = table.Column<long>(type: "bigint", nullable: false),
                    QueuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SubmittedStatus = table.Column<int>(type: "int", nullable: true),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastStatus = table.Column<int>(type: "int", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndexNowEntries", x => x.OpportunityId);
                    table.ForeignKey(
                        name: "FK_IndexNowEntries_Opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalTable: "Opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationAliases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Alias = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Canonical = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationAliases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PipelineStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    AiPausedUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AiPauseReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    LastRequeuedPromptVersionId = table.Column<int>(type: "int", nullable: true),
                    LastRequeuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TelegramChannelSettings",
                columns: table => new
                {
                    Target = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    EnabledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Actor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramChannelSettings", x => x.Target);
                });

            migrationBuilder.InsertData(
                table: "PipelineStates",
                columns: new[] { "Id", "AiPauseReason", "AiPausedUntil", "LastRequeuedAt", "LastRequeuedPromptVersionId" },
                values: new object[] { 1, "", null, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_SourceItems_RequestedPromptVersionId",
                table: "SourceItems",
                column: "RequestedPromptVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_SocialPublications_OpportunityId_Provider_PageId",
                table: "SocialPublications",
                columns: new[] { "OpportunityId", "Provider", "PageId" },
                unique: true,
                filter: "[Provider] = N'Telegram'");

            migrationBuilder.CreateIndex(
                name: "IX_SocialPublications_Provider_Status_NextAttemptAt",
                table: "SocialPublications",
                columns: new[] { "Provider", "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Runs_SourceId_Status_StartedAt",
                table: "Runs",
                columns: new[] { "SourceId", "Status", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Revisions_CapturedAt",
                table: "Revisions",
                column: "CapturedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Revisions_SourceItemId_CapturedAt",
                table: "Revisions",
                columns: new[] { "SourceItemId", "CapturedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Opportunities_Kind_NormalizedOrganization_NormalizedReference",
                table: "Opportunities",
                columns: new[] { "Kind", "NormalizedOrganization", "NormalizedReference" });

            migrationBuilder.CreateIndex(
                name: "IX_Opportunities_Status_Kind_SortDate",
                table: "Opportunities",
                columns: new[] { "Status", "Kind", "SortDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DuplicateCandidates_CandidateId",
                table: "DuplicateCandidates",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_DuplicateCandidates_OpportunityId_CandidateId",
                table: "DuplicateCandidates",
                columns: new[] { "OpportunityId", "CandidateId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DuplicateCandidates_Status_CreatedAt",
                table: "DuplicateCandidates",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IndexNowEntries_ProcessedAt_QueuedAt",
                table: "IndexNowEntries",
                columns: new[] { "ProcessedAt", "QueuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationAliases_Alias",
                table: "OrganizationAliases",
                column: "Alias",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationAliases_Canonical",
                table: "OrganizationAliases",
                column: "Canonical");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DuplicateCandidates");

            migrationBuilder.DropTable(
                name: "IndexNowEntries");

            migrationBuilder.DropTable(
                name: "OrganizationAliases");

            migrationBuilder.DropTable(
                name: "PipelineStates");

            migrationBuilder.DropTable(
                name: "TelegramChannelSettings");

            migrationBuilder.DropIndex(
                name: "IX_SourceItems_RequestedPromptVersionId",
                table: "SourceItems");

            migrationBuilder.DropIndex(
                name: "IX_SocialPublications_OpportunityId_Provider_PageId",
                table: "SocialPublications");

            migrationBuilder.DropIndex(
                name: "IX_SocialPublications_Provider_Status_NextAttemptAt",
                table: "SocialPublications");

            migrationBuilder.DropIndex(
                name: "IX_Runs_SourceId_Status_StartedAt",
                table: "Runs");

            migrationBuilder.DropIndex(
                name: "IX_Revisions_CapturedAt",
                table: "Revisions");

            migrationBuilder.DropIndex(
                name: "IX_Revisions_SourceItemId_CapturedAt",
                table: "Revisions");

            migrationBuilder.DropIndex(
                name: "IX_Opportunities_Kind_NormalizedOrganization_NormalizedReference",
                table: "Opportunities");

            migrationBuilder.DropIndex(
                name: "IX_Opportunities_Status_Kind_SortDate",
                table: "Opportunities");

            migrationBuilder.DropColumn(
                name: "SortDate",
                table: "Opportunities");

            migrationBuilder.DropColumn(
                name: "ConsecutiveFailures",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "HealthWarning",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "HealthWarningAt",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "LastVerifiedAt",
                table: "SourceItems");

            migrationBuilder.DropColumn(
                name: "Attempts",
                table: "SocialPublications");

            migrationBuilder.DropColumn(
                name: "Error",
                table: "SocialPublications");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "SocialPublications");

            migrationBuilder.DropColumn(
                name: "IsMergeTarget",
                table: "Opportunities");

            migrationBuilder.DropColumn(
                name: "NormalizedReference",
                table: "Opportunities");
        }
    }
}
