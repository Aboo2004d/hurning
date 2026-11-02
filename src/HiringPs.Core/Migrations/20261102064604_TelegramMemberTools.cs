using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HiringPs.Core.Migrations
{
    /// <inheritdoc />
    public partial class TelegramMemberTools : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TelegramMemberBroadcasts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(3500)", maxLength: 3500, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    PaidOnly = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AfterUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Completed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramMemberBroadcasts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TelegramMemberSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    SearchEnabled = table.Column<bool>(type: "bit", nullable: false),
                    PaidOnly = table.Column<bool>(type: "bit", nullable: false),
                    CityEnabled = table.Column<bool>(type: "bit", nullable: false),
                    OrganizationEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CategoryEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramMemberSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TelegramMemberBroadcasts_Completed_CreatedAt",
                table: "TelegramMemberBroadcasts",
                columns: new[] { "Completed", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TelegramMemberBroadcasts");

            migrationBuilder.DropTable(
                name: "TelegramMemberSettings");
        }
    }
}
