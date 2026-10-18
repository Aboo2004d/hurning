using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HiringPs.Core.Migrations
{
    /// <inheritdoc />
    public partial class LahzaHostedCheckout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CheckoutStartedAt",
                table: "PaymentOrders",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckoutUrl",
                table: "PaymentOrders",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Removing the durable initialization claim would permit a second checkout.
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM PaymentOrders WHERE CheckoutStartedAt IS NOT NULL) THROW 51000, 'Cannot roll back initialized payment history. Restore a reviewed backup instead.', 1;");
            migrationBuilder.DropColumn(
                name: "CheckoutStartedAt",
                table: "PaymentOrders");

            migrationBuilder.DropColumn(
                name: "CheckoutUrl",
                table: "PaymentOrders");
        }
    }
}
