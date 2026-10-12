using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HiringPs.Core.Migrations
{
    /// <inheritdoc />
    public partial class ManualRequestSourceRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SourceCollectionRequest_Sources_SourceId1",
                table: "SourceCollectionRequest");

            migrationBuilder.DropIndex(
                name: "IX_SourceCollectionRequest_SourceId1",
                table: "SourceCollectionRequest");

            migrationBuilder.DropColumn(
                name: "SourceId1",
                table: "SourceCollectionRequest");

            migrationBuilder.AddForeignKey(
                name: "FK_SourceCollectionRequest_Sources_SourceId",
                table: "SourceCollectionRequest",
                column: "SourceId",
                principalTable: "Sources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SourceCollectionRequest_Sources_SourceId",
                table: "SourceCollectionRequest");

            migrationBuilder.AddColumn<int>(
                name: "SourceId1",
                table: "SourceCollectionRequest",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_SourceCollectionRequest_SourceId1",
                table: "SourceCollectionRequest",
                column: "SourceId1");

            migrationBuilder.AddForeignKey(
                name: "FK_SourceCollectionRequest_Sources_SourceId1",
                table: "SourceCollectionRequest",
                column: "SourceId1",
                principalTable: "Sources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
