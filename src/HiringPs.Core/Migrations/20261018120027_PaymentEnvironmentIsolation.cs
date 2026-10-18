using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HiringPs.Core.Migrations
{
    /// <inheritdoc />
    public partial class PaymentEnvironmentIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GatewayEnvironment",
                table: "PaymentOrders",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "Production");

            migrationBuilder.AddColumn<string>(
                name: "GatewayEnvironment",
                table: "MemberSubscriptions",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "Production");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM PaymentOrders WHERE GatewayEnvironment <> 'Production') OR EXISTS (SELECT 1 FROM MemberSubscriptions WHERE GatewayEnvironment <> 'Production') THROW 51000, 'Cannot remove payment environment isolation with sandbox history present.', 1;");
            migrationBuilder.DropColumn(
                name: "GatewayEnvironment",
                table: "PaymentOrders");

            migrationBuilder.DropColumn(
                name: "GatewayEnvironment",
                table: "MemberSubscriptions");
        }
    }
}
