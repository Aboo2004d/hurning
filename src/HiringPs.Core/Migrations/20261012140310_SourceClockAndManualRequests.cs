using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HiringPs.Core.Migrations
{
    /// <inheritdoc />
    public partial class SourceClockAndManualRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CollectionRetryAt",
                table: "Sources",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "CollectionStartTime",
                table: "Sources",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.AddColumn<DateOnly>(
                name: "ScheduleAnchorDate",
                table: "Sources",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SourceCollectionRequest",
                columns: table => new
                {
                    SourceId = table.Column<int>(type: "int", nullable: false),
                    SourceId1 = table.Column<int>(type: "int", nullable: false),
                    RequestToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DiscoveryFinished = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Error = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceCollectionRequest", x => x.SourceId);
                    table.ForeignKey(
                        name: "FK_SourceCollectionRequest_Sources_SourceId1",
                        column: x => x.SourceId1,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SourceCollectionRequest_SourceId1",
                table: "SourceCollectionRequest",
                column: "SourceId1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SourceCollectionRequest");

            migrationBuilder.DropColumn(
                name: "CollectionRetryAt",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "CollectionStartTime",
                table: "Sources");

            migrationBuilder.DropColumn(
                name: "ScheduleAnchorDate",
                table: "Sources");
        }
    }
}
