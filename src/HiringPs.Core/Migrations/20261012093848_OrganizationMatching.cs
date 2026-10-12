using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HiringPs.Core.Migrations
{
    /// <inheritdoc />
    public partial class OrganizationMatching : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NormalizedOrganization",
                table: "Opportunities",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Opportunities_Kind_NormalizedOrganization",
                table: "Opportunities",
                columns: new[] { "Kind", "NormalizedOrganization" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Opportunities_Kind_NormalizedOrganization",
                table: "Opportunities");

            migrationBuilder.DropColumn(
                name: "NormalizedOrganization",
                table: "Opportunities");
        }
    }
}
