using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HiringPs.Core.Migrations;

public partial class SemanticDeadlinePrompt : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Freeze the exact policy text here. Copy pending customizations when
        // present; never overwrite a saved version or activate it mid-round.
        migrationBuilder.Sql("""
            DECLARE @baseId int;
            SELECT @baseId = COALESCE([PendingPromptVersionId], [ActivePromptVersionId])
            FROM [WorkflowSettings] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = 1;
            IF @baseId IS NULL
                THROW 51000, 'Prompt workflow settings are missing.', 1;
            IF NOT EXISTS (SELECT 1 FROM [PromptVersions] WHERE [Id] = @baseId AND LEN([FinalText]) > 0)
                THROW 51000, 'A complete saved prompt is required for the semantic deadline upgrade.', 1;
            IF NOT EXISTS (
                SELECT 1 FROM [PromptVersions]
                WHERE [Id] = @baseId AND CHARINDEX(N'SEMANTIC DEADLINE POLICY v1', [DateContract]) > 0
            )
            BEGIN
                DECLARE @policy nvarchar(max) = N'SEMANTIC DEADLINE POLICY v1: Determine the submission/application/registration closing date from the meaning and context of the original announcement, in any language or phrasing. There is no required keyword or exhaustive list of closing expressions. Treat any clearly stated end of the application, registration, or bid-submission window as deadline, even if the wording is unfamiliar. This is not the end of the job, training course, project, contract, or event; do not use publication, modification, opening of registration, site-visit, document-purchase, or bid-opening dates as deadline. Return the explicit date in yyyy-MM-dd and copy one exact continuous source clause into deadlineEvidence containing the date and enough context to establish the closing meaning. Do not invent a keyword, translate the evidence, infer a missing year, or invent a date. If the closing meaning or date is ambiguous, return null and explain the ambiguity; a genuinely absent deadline alone never requires review when AllowUnknownDeadline is true. An unfamiliar closing phrase alone is not a reason for review.';
                INSERT INTO [PromptVersions] (
                    [Template], [LegacySnapshot], [FinalText], [DateContract],
                    [SummaryLimit], [GeographicScope], [AllowedKinds], [AllowUnknownDeadline],
                    [Note], [CreatedAt], [CreatedBy]
                )
                SELECT [Template], [LegacySnapshot], [FinalText] + NCHAR(10) + @policy,
                    [DateContract] + NCHAR(10) + @policy,
                    [SummaryLimit], [GeographicScope], [AllowedKinds], [AllowUnknownDeadline],
                    N'اعتماد دلالة نهاية التقديم من سياق المصدر دون حصر الصياغات',
                    SYSUTCDATETIME(), N'Migration'
                FROM [PromptVersions] WHERE [Id] = @baseId;
                DECLARE @newId int = CONVERT(int, SCOPE_IDENTITY());
                UPDATE [WorkflowSettings] SET [PendingPromptVersionId] = @newId WHERE [Id] = 1;
                INSERT INTO [Audit] ([Actor], [Action], [Detail], [At])
                VALUES (N'Migration', N'Prompt.SemanticDeadlineQueued',
                    N'Prompt version ' + CONVERT(nvarchar(20), @newId), SYSUTCDATETIME());
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Keep immutable prompt history and its usage references. To restore an
        // earlier policy, inspect a prior version in the admin UI and save a copy.
    }
}
