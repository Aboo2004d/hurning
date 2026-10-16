using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HiringPs.Core.Migrations
{
    /// <inheritdoc />
    public partial class ToolMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ToolCheckRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ActiveSlot = table.Column<int>(type: "int", nullable: true),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    WorkerId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolCheckRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ToolWorkers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Host = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Account = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Collecting = table.Column<bool>(type: "bit", nullable: false),
                    LastLoopAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastSuccessAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastFailureAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ErrorType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolWorkers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ToolCheckResults",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<long>(type: "bigint", nullable: false),
                    Tool = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    WorkerId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CheckedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolCheckResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ToolCheckResults_ToolCheckRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "ToolCheckRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ToolCheckRequests_ActiveSlot",
                table: "ToolCheckRequests",
                column: "ActiveSlot",
                unique: true,
                filter: "[ActiveSlot] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ToolCheckRequests_Kind_RequestedAt",
                table: "ToolCheckRequests",
                columns: new[] { "Kind", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ToolCheckResults_RequestId",
                table: "ToolCheckResults",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ToolCheckResults_Tool_CheckedAt",
                table: "ToolCheckResults",
                columns: new[] { "Tool", "CheckedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ToolWorkers_At",
                table: "ToolWorkers",
                column: "At");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ToolCheckResults");

            migrationBuilder.DropTable(
                name: "ToolWorkers");

            migrationBuilder.DropTable(
                name: "ToolCheckRequests");
        }
    }
}
