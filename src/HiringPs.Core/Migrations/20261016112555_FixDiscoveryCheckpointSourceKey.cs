using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HiringPs.Core.Migrations;

public partial class FixDiscoveryCheckpointSourceKey : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        RebuildCheckpointTable(migrationBuilder, identity: false);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        RebuildCheckpointTable(migrationBuilder, identity: true);

    // SQL Server cannot change IDENTITY with ALTER COLUMN. Copy every checkpoint
    // inside the migration transaction; only rowversion tokens are regenerated.
    private static void RebuildCheckpointTable(MigrationBuilder migrationBuilder, bool identity)
    {
        var identityClause = identity ? "IDENTITY(1, 1)" : "";
        var enableIdentityInsert = identity
            ? "SET IDENTITY_INSERT [DiscoveryCheckpoints_Rebuild] ON;"
            : "";
        var disableIdentityInsert = identity
            ? "SET IDENTITY_INSERT [DiscoveryCheckpoints_Rebuild] OFF;"
            : "";
        migrationBuilder.Sql($"""
            CREATE TABLE [DiscoveryCheckpoints_Rebuild] (
                [SourceId] int {identityClause} NOT NULL,
                [ConfigurationHash] nvarchar(64) NOT NULL,
                [NextUrl] nvarchar(1500) NOT NULL,
                [CompletedPages] int NOT NULL,
                [ReplayClicks] int NOT NULL,
                [Completed] bit NOT NULL,
                [Found] int NOT NULL,
                [VisitedUrlsJson] nvarchar(max) NOT NULL,
                [UpdatedAt] datetimeoffset NOT NULL,
                [Version] rowversion NOT NULL,
                CONSTRAINT [PK_DiscoveryCheckpoints_Rebuild] PRIMARY KEY ([SourceId])
            );
            {enableIdentityInsert}
            INSERT INTO [DiscoveryCheckpoints_Rebuild] (
                [SourceId], [ConfigurationHash], [NextUrl], [CompletedPages],
                [ReplayClicks], [Completed], [Found], [VisitedUrlsJson], [UpdatedAt]
            )
            SELECT [SourceId], [ConfigurationHash], [NextUrl], [CompletedPages],
                [ReplayClicks], [Completed], [Found], [VisitedUrlsJson], [UpdatedAt]
            FROM [DiscoveryCheckpoints] WITH (TABLOCKX, HOLDLOCK);
            {disableIdentityInsert}
            DROP TABLE [DiscoveryCheckpoints];
            EXEC sp_rename N'DiscoveryCheckpoints_Rebuild', N'DiscoveryCheckpoints';
            EXEC sp_rename N'PK_DiscoveryCheckpoints_Rebuild', N'PK_DiscoveryCheckpoints', N'OBJECT';
            """);
    }
}
