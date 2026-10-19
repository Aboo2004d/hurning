using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HiringPs.Core.Migrations
{
    /// <inheritdoc />
    public partial class VersionedSocialTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "TemplateVersionId",
                table: "SocialPublications",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SocialTemplateVersions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Provider = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 50000, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialTemplateVersions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SocialPublications_TemplateVersionId",
                table: "SocialPublications",
                column: "TemplateVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_SocialTemplateVersions_Provider_Scope",
                table: "SocialTemplateVersions",
                columns: new[] { "Provider", "Scope" },
                unique: true,
                filter: "[Active] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_SocialTemplateVersions_Provider_Scope_Number",
                table: "SocialTemplateVersions",
                columns: new[] { "Provider", "Scope", "Number" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SocialPublications_SocialTemplateVersions_TemplateVersionId",
                table: "SocialPublications",
                column: "TemplateVersionId",
                principalTable: "SocialTemplateVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            // Initial templates are versioned data, never overwritten at startup.
            migrationBuilder.Sql("""
                INSERT INTO [SocialTemplateVersions] ([Provider], [Scope], [Number], [Active], [Body], [Actor], [CreatedAt]) VALUES
                (N'Facebook', N'general', 1, 1, N'📢 {{kind}}

                💼 {{title}}
                🏢 {{organization}}
                📍 {{location}}
                📅 آخر موعد للتقديم: {{deadline}}

                {{summary}}

                للتفاصيل وطريقة التقديم 👇', N'System', SYSUTCDATETIME()),
                (N'Facebook', N'Job', 1, 1, N'📢 {{kind}}

                💼 {{title}}
                🏢 {{organization}}
                📍 {{location}}
                📅 آخر موعد للتقديم: {{deadline}}

                {{summary}}

                للتفاصيل وطريقة التقديم 👇', N'System', SYSUTCDATETIME()),
                (N'Facebook', N'Training', 1, 1, N'📢 {{kind}}

                💼 {{title}}
                🏢 {{organization}}
                📍 {{location}}
                📅 آخر موعد للتقديم: {{deadline}}

                {{summary}}

                للتفاصيل وطريقة التقديم 👇', N'System', SYSUTCDATETIME()),
                (N'Facebook', N'Tender', 1, 1, N'📢 {{kind}}

                💼 {{title}}
                🏢 {{organization}}
                📍 {{location}}
                📅 آخر موعد للتقديم: {{deadline}}

                {{summary}}

                للتفاصيل وطريقة التقديم 👇', N'System', SYSUTCDATETIME()),
                (N'Facebook', N'ProcurementNotice', 1, 1, N'📢 {{kind}}

                💼 {{title}}
                🏢 {{organization}}
                📍 {{location}}
                📅 آخر موعد للتقديم: {{deadline}}

                {{summary}}

                للتفاصيل وطريقة التقديم 👇', N'System', SYSUTCDATETIME()),
                (N'LinkedIn', N'general', 1, 1, N'📢 {{kind}}

                💼 {{title}}
                🏢 {{organization}}
                📍 {{location}}
                📅 آخر موعد للتقديم: {{deadline}}

                {{summary}}

                للتفاصيل وطريقة التقديم 👇', N'System', SYSUTCDATETIME()),
                (N'LinkedIn', N'Job', 1, 1, N'📢 {{kind}}

                💼 {{title}}
                🏢 {{organization}}
                📍 {{location}}
                📅 آخر موعد للتقديم: {{deadline}}

                {{summary}}

                للتفاصيل وطريقة التقديم 👇', N'System', SYSUTCDATETIME()),
                (N'LinkedIn', N'Training', 1, 1, N'📢 {{kind}}

                💼 {{title}}
                🏢 {{organization}}
                📍 {{location}}
                📅 آخر موعد للتقديم: {{deadline}}

                {{summary}}

                للتفاصيل وطريقة التقديم 👇', N'System', SYSUTCDATETIME()),
                (N'LinkedIn', N'Tender', 1, 1, N'📢 {{kind}}

                💼 {{title}}
                🏢 {{organization}}
                📍 {{location}}
                📅 آخر موعد للتقديم: {{deadline}}

                {{summary}}

                للتفاصيل وطريقة التقديم 👇', N'System', SYSUTCDATETIME()),
                (N'LinkedIn', N'ProcurementNotice', 1, 1, N'📢 {{kind}}

                💼 {{title}}
                🏢 {{organization}}
                📍 {{location}}
                📅 آخر موعد للتقديم: {{deadline}}

                {{summary}}

                للتفاصيل وطريقة التقديم 👇', N'System', SYSUTCDATETIME());
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SocialPublications_SocialTemplateVersions_TemplateVersionId",
                table: "SocialPublications");

            migrationBuilder.DropTable(
                name: "SocialTemplateVersions");

            migrationBuilder.DropIndex(
                name: "IX_SocialPublications_TemplateVersionId",
                table: "SocialPublications");

            migrationBuilder.DropColumn(
                name: "TemplateVersionId",
                table: "SocialPublications");
        }
    }
}
