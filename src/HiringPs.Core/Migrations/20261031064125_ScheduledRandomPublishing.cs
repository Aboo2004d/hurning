using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HiringPs.Core.Migrations
{
    /// <inheritdoc />
    public partial class ScheduledRandomPublishing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RandomPublicationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Slot = table.Column<int>(type: "int", nullable: false),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OpportunityId = table.Column<long>(type: "bigint", nullable: true),
                    PageId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    ResultCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RandomPublicationRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RandomPublicationRuns_Opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalTable: "Opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RandomPublicationSettings",
                columns: table => new
                {
                    Provider = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    DailyCount = table.Column<int>(type: "int", nullable: false),
                    MaxRemainingDays = table.Column<int>(type: "int", nullable: false),
                    TimesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RandomPublicationSettings", x => x.Provider);
                    table.CheckConstraint("CK_RandomSetting_Count", "[DailyCount] BETWEEN 1 AND 24");
                    table.CheckConstraint("CK_RandomSetting_Days", "[MaxRemainingDays] BETWEEN 0 AND 3650");
                    table.CheckConstraint("CK_RandomSetting_Provider", "[Provider] IN ('Facebook','LinkedInPersonal','LinkedIn')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_RandomPublicationRuns_CreatedAt",
                table: "RandomPublicationRuns",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RandomPublicationRuns_OpportunityId",
                table: "RandomPublicationRuns",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_RandomPublicationRuns_Provider_Day_Slot",
                table: "RandomPublicationRuns",
                columns: new[] { "Provider", "Day", "Slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RandomPublicationRuns_Provider_OpportunityId",
                table: "RandomPublicationRuns",
                columns: new[] { "Provider", "OpportunityId" },
                unique: true,
                filter: "[OpportunityId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RandomPublicationRuns");

            migrationBuilder.DropTable(
                name: "RandomPublicationSettings");
        }
    }
}
