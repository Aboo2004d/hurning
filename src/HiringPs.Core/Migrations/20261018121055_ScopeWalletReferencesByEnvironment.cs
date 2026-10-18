using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HiringPs.Core.Migrations
{
    /// <inheritdoc />
    public partial class ScopeWalletReferencesByEnvironment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WalletPaymentSessions_SessionKeyHash",
                table: "WalletPaymentSessions");

            migrationBuilder.DropIndex(
                name: "IX_WalletPaymentSessions_TransactionId",
                table: "WalletPaymentSessions");

            migrationBuilder.DropIndex(
                name: "IX_PaymentOrders_Gateway_ProviderReference",
                table: "PaymentOrders");

            migrationBuilder.AddColumn<string>(
                name: "GatewayEnvironment",
                table: "WalletPaymentSessions",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "Production");

            migrationBuilder.Sql("UPDATE sessions SET GatewayEnvironment = orders.GatewayEnvironment FROM WalletPaymentSessions AS sessions INNER JOIN PaymentOrders AS orders ON orders.Id = sessions.OrderId;");

            migrationBuilder.CreateIndex(
                name: "IX_WalletPaymentSessions_GatewayEnvironment_SessionKeyHash",
                table: "WalletPaymentSessions",
                columns: new[] { "GatewayEnvironment", "SessionKeyHash" },
                unique: true,
                filter: "[SessionKeyHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WalletPaymentSessions_GatewayEnvironment_TransactionId",
                table: "WalletPaymentSessions",
                columns: new[] { "GatewayEnvironment", "TransactionId" },
                unique: true,
                filter: "[TransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentOrders_Gateway_GatewayEnvironment_ProviderReference",
                table: "PaymentOrders",
                columns: new[] { "Gateway", "GatewayEnvironment", "ProviderReference" },
                unique: true,
                filter: "[ProviderReference] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM WalletPaymentSessions WHERE GatewayEnvironment <> 'Production') OR EXISTS (SELECT 1 FROM PaymentOrders WHERE GatewayEnvironment <> 'Production') THROW 51000, 'Cannot remove environment-scoped financial references while sandbox history exists.', 1;");
            migrationBuilder.DropIndex(
                name: "IX_WalletPaymentSessions_GatewayEnvironment_SessionKeyHash",
                table: "WalletPaymentSessions");

            migrationBuilder.DropIndex(
                name: "IX_WalletPaymentSessions_GatewayEnvironment_TransactionId",
                table: "WalletPaymentSessions");

            migrationBuilder.DropIndex(
                name: "IX_PaymentOrders_Gateway_GatewayEnvironment_ProviderReference",
                table: "PaymentOrders");

            migrationBuilder.DropColumn(
                name: "GatewayEnvironment",
                table: "WalletPaymentSessions");

            migrationBuilder.CreateIndex(
                name: "IX_WalletPaymentSessions_SessionKeyHash",
                table: "WalletPaymentSessions",
                column: "SessionKeyHash",
                unique: true,
                filter: "[SessionKeyHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WalletPaymentSessions_TransactionId",
                table: "WalletPaymentSessions",
                column: "TransactionId",
                unique: true,
                filter: "[TransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentOrders_Gateway_ProviderReference",
                table: "PaymentOrders",
                columns: new[] { "Gateway", "ProviderReference" },
                unique: true,
                filter: "[ProviderReference] IS NOT NULL");
        }
    }
}
