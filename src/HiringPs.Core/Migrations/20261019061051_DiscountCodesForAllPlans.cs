using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HiringPs.Core.Migrations
{
    /// <inheritdoc />
    public partial class DiscountCodesForAllPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "PlanId",
                table: "DiscountCodes",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Never silently assign global discounts (including historical orders) to plan 0.
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM [DiscountCodes] WHERE [PlanId] IS NULL) "
                + "THROW 51000, 'Cannot revert global discounts while all-plan codes exist.', 1;"
            );
            migrationBuilder.AlterColumn<int>(
                name: "PlanId",
                table: "DiscountCodes",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
