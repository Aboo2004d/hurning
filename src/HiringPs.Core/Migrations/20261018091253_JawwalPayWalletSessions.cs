using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HiringPs.Core.Migrations
{
    /// <inheritdoc />
    public partial class JawwalPayWalletSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WalletPaymentSessions",
                columns: table => new
                {
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ProtectedSessionKey = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    SessionKeyHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    InitiationRejected = table.Column<bool>(type: "bit", nullable: false),
                    ConfirmationStartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ConfirmationRejected = table.Column<bool>(type: "bit", nullable: false),
                    TransactionId = table.Column<long>(type: "bigint", nullable: true),
                    JawwalReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ConfirmedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    JawwalFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PlatformFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    NetCredited = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletPaymentSessions", x => x.OrderId);
                    table.ForeignKey(
                        name: "FK_WalletPaymentSessions_PaymentOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "PaymentOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WalletPaymentSessions_CreatedAt",
                table: "WalletPaymentSessions",
                column: "CreatedAt");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM WalletPaymentSessions) THROW 51000, 'Cannot drop wallet session and confirmation history. Restore a reviewed backup instead.', 1;");
            migrationBuilder.DropTable(
                name: "WalletPaymentSessions");
        }
    }
}
