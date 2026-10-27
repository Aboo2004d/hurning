using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HiringPs.Core.Migrations
{
    /// <inheritdoc />
    public partial class PublicCatalogRevision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PublicCatalogRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicCatalogRevisions", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "PublicCatalogRevisions",
                columns: new[] { "Id", "Version" },
                values: new object[] { 1, 0L });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PublicCatalogRevisions");
        }
    }
}
