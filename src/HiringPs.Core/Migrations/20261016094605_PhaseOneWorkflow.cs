using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HiringPs.Core.Migrations
{
    /// <inheritdoc />
    public partial class PhaseOneWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExtractionPromptVersionId",
                table: "SourceItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequestedPromptVersionId",
                table: "SourceItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationSearchText",
                table: "Opportunities",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PromptVersionId",
                table: "Opportunities",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SearchText",
                table: "Opportunities",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SourceModifiedAt",
                table: "Opportunities",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DiscoveryCheckpoints",
                columns: table => new
                {
                    SourceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConfigurationHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NextUrl = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: false),
                    CompletedPages = table.Column<int>(type: "int", nullable: false),
                    ReplayClicks = table.Column<int>(type: "int", nullable: false),
                    Completed = table.Column<bool>(type: "bit", nullable: false),
                    Found = table.Column<int>(type: "int", nullable: false),
                    VisitedUrlsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscoveryCheckpoints", x => x.SourceId);
                });

            migrationBuilder.CreateTable(
                name: "MergeOperations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DuplicateId = table.Column<long>(type: "bigint", nullable: false),
                    TargetId = table.Column<long>(type: "bigint", nullable: false),
                    OriginalStatus = table.Column<int>(type: "int", nullable: false),
                    DuplicateOverride = table.Column<bool>(type: "bit", nullable: false),
                    TargetOverride = table.Column<bool>(type: "bit", nullable: false),
                    OriginalReviewReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LinkIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DuplicateVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    DuplicateBeforeVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    TargetBeforeVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    TargetVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UndoneAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Actor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MergeOperations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpportunityDates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OpportunityId = table.Column<long>(type: "bigint", nullable: false),
                    SourceItemId = table.Column<long>(type: "bigint", nullable: false),
                    PromptVersionId = table.Column<int>(type: "int", nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Evidence = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EvidenceHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsExtension = table.Column<bool>(type: "bit", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpportunityDates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpportunityDates_Opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalTable: "Opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PromptVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Template = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LegacySnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FinalText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateContract = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SummaryLimit = table.Column<int>(type: "int", nullable: false),
                    GeographicScope = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AllowedKinds = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AllowUnknownDeadline = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RetentionPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    RevisionDays = table.Column<int>(type: "int", nullable: false),
                    DiagnosticDays = table.Column<int>(type: "int", nullable: false),
                    LastCleanupAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastDeletedRevisions = table.Column<int>(type: "int", nullable: false),
                    LastDeletedDiagnostics = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetentionPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiUsage",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PromptVersionId = table.Column<int>(type: "int", nullable: false),
                    SourceItemId = table.Column<long>(type: "bigint", nullable: true),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PromptHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    HttpStatus = table.Column<int>(type: "int", nullable: true),
                    Success = table.Column<bool>(type: "bit", nullable: true),
                    ErrorType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InputTokens = table.Column<int>(type: "int", nullable: true),
                    OutputTokens = table.Column<int>(type: "int", nullable: true),
                    CachedTokens = table.Column<int>(type: "int", nullable: true),
                    TotalTokens = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiUsage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiUsage_PromptVersions_PromptVersionId",
                        column: x => x.PromptVersionId,
                        principalTable: "PromptVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    ActivePromptVersionId = table.Column<int>(type: "int", nullable: false),
                    PendingPromptVersionId = table.Column<int>(type: "int", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowSettings_PromptVersions_ActivePromptVersionId",
                        column: x => x.ActivePromptVersionId,
                        principalTable: "PromptVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowSettings_PromptVersions_PendingPromptVersionId",
                        column: x => x.PendingPromptVersionId,
                        principalTable: "PromptVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "RetentionPolicies",
                columns: new[] { "Id", "DiagnosticDays", "Enabled", "LastCleanupAt", "LastDeletedDiagnostics", "LastDeletedRevisions", "RevisionDays" },
                values: new object[] { 1, 30, true, null, 0, 0, 90 });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsage_PromptVersionId",
                table: "AiUsage",
                column: "PromptVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_AiUsage_StartedAt_PromptVersionId_Model",
                table: "AiUsage",
                columns: new[] { "StartedAt", "PromptVersionId", "Model" });

            migrationBuilder.CreateIndex(
                name: "IX_MergeOperations_DuplicateId_TargetId_UndoneAt",
                table: "MergeOperations",
                columns: new[] { "DuplicateId", "TargetId", "UndoneAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OpportunityDates_OpportunityId_Kind_EvidenceHash",
                table: "OpportunityDates",
                columns: new[] { "OpportunityId", "Kind", "EvidenceHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSettings_ActivePromptVersionId",
                table: "WorkflowSettings",
                column: "ActivePromptVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSettings_PendingPromptVersionId",
                table: "WorkflowSettings",
                column: "PendingPromptVersionId");
            migrationBuilder.Sql("""
-- Backfill search fields only; original announcement wording remains unchanged.
DECLARE @Aliases TABLE ([GroupId] int, [Alias] nvarchar(100));
INSERT INTO @Aliases ([GroupId], [Alias]) VALUES
    (0, N'رام الله'),
    (0, N'ramallah'),
    (0, N'ram allah'),
    (1, N'البيرة'),
    (1, N'al bireh'),
    (1, N'el bireh'),
    (2, N'غزة'),
    (2, N'gaza'),
    (2, N'gaza strip'),
    (3, N'القدس'),
    (3, N'jerusalem'),
    (3, N'east jerusalem'),
    (3, N'القدس الشرقية'),
    (4, N'الخليل'),
    (4, N'hebron'),
    (4, N'al khalil'),
    (5, N'نابلس'),
    (5, N'nablus'),
    (6, N'جنين'),
    (6, N'jenin'),
    (7, N'بيت لحم'),
    (7, N'bethlehem'),
    (7, N'bayt lahm'),
    (8, N'اريحا'),
    (8, N'jericho'),
    (8, N'ariha'),
    (9, N'طولكرم'),
    (9, N'tulkarm'),
    (9, N'tulkarem'),
    (10, N'قلقيلية'),
    (10, N'qalqilya'),
    (10, N'qalqilia'),
    (11, N'طوباس'),
    (11, N'tubas'),
    (12, N'سلفيت'),
    (12, N'salfit'),
    (13, N'فلسطين'),
    (13, N'palestine'),
    (14, N'الضفة الغربية'),
    (14, N'west bank');

WITH [Inputs] AS (
    SELECT o.[Id], fields.[Field], fields.[RawText]
    FROM [Opportunities] AS o
    CROSS APPLY (VALUES
        (0, o.[Locations]),
        (1, CONCAT(o.[Title], N' ', o.[Organization], N' ', o.[Summary], N' ', o.[Locations]))
    ) AS fields([Field], [RawText])
), [Normalized] AS (
    SELECT [Id], [Field], LTRIM(RTRIM(
        REPLACE(
        REPLACE(
        REPLACE(
        REPLACE(
        REPLACE(
        REPLACE(
        REPLACE(
        REPLACE(
        REPLACE(
        REPLACE(
        REPLACE(
        REPLACE(
        REPLACE(
        REPLACE(
        LOWER(TRANSLATE([RawText], N'أإآى-/،,.:;()[]{}…', N'اااي              ')), N'ـ', N''), N'ً', N''), N'ٌ', N''), N'ٍ', N''), N'َ', N''), N'ُ', N''), N'ِ', N''), N'ّ', N''), N'ْ', N''), N'  ', N' '), N'  ', N' '), N'  ', N' '), N'  ', N' '), N'  ', N' ')
    )) AS [Text]
    FROM [Inputs]
)
UPDATE o SET
    [LocationSearchText] = location.[Text] + N' ' + COALESCE(aliases.[Text], N''),
    [SearchText] = body.[Text] + N' ' + location.[Text] + N' ' + COALESCE(aliases.[Text], N'')
FROM [Opportunities] AS o
JOIN [Normalized] AS location ON location.[Id] = o.[Id] AND location.[Field] = 0
JOIN [Normalized] AS body ON body.[Id] = o.[Id] AND body.[Field] = 1
OUTER APPLY (
    SELECT STRING_AGG(CAST(a.[Alias] AS nvarchar(max)), N' ') AS [Text]
    FROM @Aliases AS a
    WHERE EXISTS (
        SELECT 1 FROM @Aliases AS matched
        WHERE matched.[GroupId] = a.[GroupId]
          AND CHARINDEX(N' ' + matched.[Alias] + N' ', N' ' + location.[Text] + N' ') > 0
    )
) AS aliases;
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiUsage");

            migrationBuilder.DropTable(
                name: "DiscoveryCheckpoints");

            migrationBuilder.DropTable(
                name: "MergeOperations");

            migrationBuilder.DropTable(
                name: "OpportunityDates");

            migrationBuilder.DropTable(
                name: "RetentionPolicies");

            migrationBuilder.DropTable(
                name: "WorkflowSettings");

            migrationBuilder.DropTable(
                name: "PromptVersions");

            migrationBuilder.DropColumn(
                name: "ExtractionPromptVersionId",
                table: "SourceItems");

            migrationBuilder.DropColumn(
                name: "RequestedPromptVersionId",
                table: "SourceItems");

            migrationBuilder.DropColumn(
                name: "LocationSearchText",
                table: "Opportunities");

            migrationBuilder.DropColumn(
                name: "PromptVersionId",
                table: "Opportunities");

            migrationBuilder.DropColumn(
                name: "SearchText",
                table: "Opportunities");

            migrationBuilder.DropColumn(
                name: "SourceModifiedAt",
                table: "Opportunities");
        }
    }
}
