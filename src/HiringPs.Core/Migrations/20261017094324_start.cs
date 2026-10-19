using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HiringPs.Core.Migrations
{
    /// <inheritdoc />
    public partial class start : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            /* Historical migration retained during the branch merge. Existing dbo
               tables need no transfer; transferring dbo to itself fails on SQL Server. */
            foreach (var table in new[] { "Sources", "SourceLinks", "SourceItems", "SourceCollectionRequest", "SitePages", "Runs", "Revisions", "Reports", "Opportunities", "Heartbeats", "ContactMessages", "Audit", "AspNetUserTokens", "AspNetUsers", "AspNetUserRoles", "AspNetUserLogins", "AspNetUserClaims", "AspNetRoles", "AspNetRoleClaims" })
            {
                migrationBuilder.Sql($"""
                    IF OBJECT_ID(N'[dbo].[{table}]', N'U') IS NULL
                    BEGIN
                        DECLARE @sourceSchema_{table} sysname;
                        IF (SELECT COUNT(*) FROM sys.tables WHERE [name] = N'{table}') <> 1
                            THROW 51000, 'Cannot resolve a unique source table for dbo migration.', 1;
                        SELECT @sourceSchema_{table} = SCHEMA_NAME([schema_id]) FROM sys.tables WHERE [name] = N'{table}';
                        DECLARE @transfer_{table} nvarchar(max) = N'ALTER SCHEMA [dbo] TRANSFER ' + QUOTENAME(@sourceSchema_{table}) + N'.' + QUOTENAME(N'{table}');
                        EXEC sp_executesql @transfer_{table};
                    END;
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "Sources",
                schema: "dbo",
                newName: "Sources");

            migrationBuilder.RenameTable(
                name: "SourceLinks",
                schema: "dbo",
                newName: "SourceLinks");

            migrationBuilder.RenameTable(
                name: "SourceItems",
                schema: "dbo",
                newName: "SourceItems");

            migrationBuilder.RenameTable(
                name: "SourceCollectionRequest",
                schema: "dbo",
                newName: "SourceCollectionRequest");

            migrationBuilder.RenameTable(
                name: "SitePages",
                schema: "dbo",
                newName: "SitePages");

            migrationBuilder.RenameTable(
                name: "Runs",
                schema: "dbo",
                newName: "Runs");

            migrationBuilder.RenameTable(
                name: "Revisions",
                schema: "dbo",
                newName: "Revisions");

            migrationBuilder.RenameTable(
                name: "Reports",
                schema: "dbo",
                newName: "Reports");

            migrationBuilder.RenameTable(
                name: "Opportunities",
                schema: "dbo",
                newName: "Opportunities");

            migrationBuilder.RenameTable(
                name: "Heartbeats",
                schema: "dbo",
                newName: "Heartbeats");

            migrationBuilder.RenameTable(
                name: "ContactMessages",
                schema: "dbo",
                newName: "ContactMessages");

            migrationBuilder.RenameTable(
                name: "Audit",
                schema: "dbo",
                newName: "Audit");

            migrationBuilder.RenameTable(
                name: "AspNetUserTokens",
                schema: "dbo",
                newName: "AspNetUserTokens");

            migrationBuilder.RenameTable(
                name: "AspNetUsers",
                schema: "dbo",
                newName: "AspNetUsers");

            migrationBuilder.RenameTable(
                name: "AspNetUserRoles",
                schema: "dbo",
                newName: "AspNetUserRoles");

            migrationBuilder.RenameTable(
                name: "AspNetUserLogins",
                schema: "dbo",
                newName: "AspNetUserLogins");

            migrationBuilder.RenameTable(
                name: "AspNetUserClaims",
                schema: "dbo",
                newName: "AspNetUserClaims");

            migrationBuilder.RenameTable(
                name: "AspNetRoles",
                schema: "dbo",
                newName: "AspNetRoles");

            migrationBuilder.RenameTable(
                name: "AspNetRoleClaims",
                schema: "dbo",
                newName: "AspNetRoleClaims");
        }
    }
}
