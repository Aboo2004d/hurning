using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HiringPs.Core.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceApplicationErrorsWithElmah : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Native ElmahCore.Sql 2.1.2 schema. No EF entity or custom error store.
            migrationBuilder.Sql("""
                CREATE TABLE [dbo].[ELMAH_Error] (
                    [ErrorId] uniqueidentifier NOT NULL CONSTRAINT [DF_ELMAH_Error_ErrorId] DEFAULT NEWID(),
                    [Application] nvarchar(60) NOT NULL,
                    [Host] nvarchar(50) NOT NULL,
                    [Type] nvarchar(100) NOT NULL,
                    [Source] nvarchar(60) NOT NULL,
                    [Message] nvarchar(max) NOT NULL,
                    [User] nvarchar(50) NOT NULL,
                    [StatusCode] int NOT NULL,
                    [TimeUtc] datetime NOT NULL,
                    [Sequence] int IDENTITY(1,1) NOT NULL,
                    [AllXml] nvarchar(max) NOT NULL,
                    CONSTRAINT [PK_ELMAH_Error] PRIMARY KEY NONCLUSTERED ([ErrorId])
                );
                CREATE NONCLUSTERED INDEX [IX_ELMAH_Error_App_Time_Seq]
                    ON [dbo].[ELMAH_Error] ([Application], [TimeUtc] DESC, [Sequence] DESC);
                """);
            // The previous diagnostics table is intentionally retired, as requested.
            migrationBuilder.DropTable(
                name: "ApplicationErrors");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE [dbo].[ELMAH_Error];");
            migrationBuilder.CreateTable(
                name: "ApplicationErrors",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Application = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    At = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExceptionType = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Level = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    Path = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    TraceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationErrors", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationErrors_At",
                table: "ApplicationErrors",
                column: "At");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationErrors_TraceId",
                table: "ApplicationErrors",
                column: "TraceId");
        }
    }
}
