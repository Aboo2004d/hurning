using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HiringPs.Core.Migrations
{
    /// <inheritdoc />
    public partial class ExpandMemberPreferenceCatalogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "LocationsJson",
                table: "MemberPreferences",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AlterColumn<string>(
                name: "CategoriesJson",
                table: "MemberPreferences",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Never truncate existing selections when reverting to the old schema.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [MemberPreferences]
                    WHERE DATALENGTH([CategoriesJson]) > 8000 OR DATALENGTH([LocationsJson]) > 8000)
                    THROW 51000, 'Cannot revert preference storage: existing selections exceed 4000 characters.', 1;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "LocationsJson",
                table: "MemberPreferences",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "CategoriesJson",
                table: "MemberPreferences",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }
    }
}
