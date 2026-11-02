using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HiringPs.Core.Migrations
{
    /// <inheritdoc />
    public partial class ConfigurableFreePreferenceLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MemberPreferenceSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    NonSubscriberCategoryLimit = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberPreferenceSettings", x => x.Id);
                    table.CheckConstraint("CK_MemberPreferenceSettings_Limit", "[NonSubscriberCategoryLimit] BETWEEN 0 AND 1000");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemberPreferenceSettings");
        }
    }
}
