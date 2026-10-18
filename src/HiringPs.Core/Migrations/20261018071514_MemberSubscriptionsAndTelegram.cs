using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HiringPs.Core.Migrations
{
    /// <inheritdoc />
    public partial class MemberSubscriptionsAndTelegram : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MemberProfiles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Mobile = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: true),
                    TelegramChatId = table.Column<long>(type: "bigint", nullable: true),
                    Activated = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    NotificationsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    SavedRemindersEnabled = table.Column<bool>(type: "bit", nullable: false),
                    NotificationsSince = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Suspended = table.Column<bool>(type: "bit", nullable: false),
                    SuspensionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    SuspendedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FreezeSubscription = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberProfiles", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_MemberProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MembershipRuntimeStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    LockedUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReminderAfterSubscription = table.Column<long>(type: "bigint", nullable: false),
                    ReminderAfterSaved = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipRuntimeStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpportunityAccessRules",
                columns: table => new
                {
                    Kind = table.Column<int>(type: "int", nullable: false),
                    PublicDetails = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpportunityAccessRules", x => x.Kind);
                });

            migrationBuilder.CreateTable(
                name: "OpportunityPublications",
                columns: table => new
                {
                    OpportunityId = table.Column<long>(type: "bigint", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AfterUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Completed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpportunityPublications", x => x.OpportunityId);
                    table.ForeignKey(
                        name: "FK_OpportunityPublications_Opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalTable: "Opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentGatewaySettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentGatewaySettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "TelegramInbox",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Json = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockedUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramInbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MemberChallenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Purpose = table.Column<int>(type: "int", nullable: false),
                    LinkHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ExpectedMobile = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: true),
                    ChatId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CodeSentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberChallenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberChallenges_MemberProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "MemberProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MemberMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    EventKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    OpportunityId = table.Column<long>(type: "bigint", nullable: true),
                    EventAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    AvailableAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockedUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberMessages_MemberProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "MemberProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MemberPreferences",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Kinds = table.Column<int>(type: "int", nullable: false),
                    CategoriesJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    LocationsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberPreferences", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_MemberPreferences_MemberProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "MemberProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SavedOpportunities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    OpportunityId = table.Column<long>(type: "bigint", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedOpportunities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedOpportunities_MemberProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "MemberProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SavedOpportunities_Opportunities_OpportunityId",
                        column: x => x.OpportunityId,
                        principalTable: "Opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DiscountCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    Percent = table.Column<int>(type: "int", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    MaximumUses = table.Column<int>(type: "int", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscountCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MembershipPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Visible = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CurrentVersionId = table.Column<int>(type: "int", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MembershipPlanVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlanId = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    DescriptionAr = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DescriptionEn = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    BadgeAr = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    BadgeEn = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    PriceIls = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Days = table.Column<int>(type: "int", nullable: false),
                    DetailKinds = table.Column<int>(type: "int", nullable: false),
                    AlertKinds = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipPlanVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MembershipPlanVersions_MembershipPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "MembershipPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    PlanVersionId = table.Column<int>(type: "int", nullable: false),
                    DiscountCodeId = table.Column<int>(type: "int", nullable: true),
                    DiscountPercent = table.Column<int>(type: "int", nullable: false),
                    AmountIls = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Gateway = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ProviderReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    State = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastCheckedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TermsVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TermsAcceptedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DecisionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    PaymentEvidence = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentOrders_DiscountCodes_DiscountCodeId",
                        column: x => x.DiscountCodeId,
                        principalTable: "DiscountCodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentOrders_MemberProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "MemberProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentOrders_MembershipPlanVersions_PlanVersionId",
                        column: x => x.PlanVersionId,
                        principalTable: "MembershipPlanVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MemberSubscriptions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    PlanVersionId = table.Column<int>(type: "int", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Cancelled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberSubscriptions_MemberProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "MemberProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemberSubscriptions_MembershipPlanVersions_PlanVersionId",
                        column: x => x.PlanVersionId,
                        principalTable: "MembershipPlanVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemberSubscriptions_PaymentOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "PaymentOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentReviews",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AttachmentName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Resolution = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentReviews_PaymentOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "PaymentOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "MembershipRuntimeStates",
                columns: new[] { "Id", "LockedUntil", "ReminderAfterSaved", "ReminderAfterSubscription" },
                values: new object[] { 1, null, 0L, 0L });

            migrationBuilder.InsertData(
                table: "OpportunityAccessRules",
                columns: new[] { "Kind", "PublicDetails" },
                values: new object[,]
                {
                    { 0, true },
                    { 1, true },
                    { 2, true },
                    { 3, true }
                });

            migrationBuilder.InsertData(
                table: "PaymentGatewaySettings",
                columns: new[] { "Key", "Enabled" },
                values: new object[,]
                {
                    { "JawwalPay", false },
                    { "Lahza", false }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiscountCodes_Code",
                table: "DiscountCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscountCodes_PlanId",
                table: "DiscountCodes",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberChallenges_LinkHash",
                table: "MemberChallenges",
                column: "LinkHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberChallenges_UserId",
                table: "MemberChallenges",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberMessages_SentAt_CancelledAt_AvailableAt",
                table: "MemberMessages",
                columns: new[] { "SentAt", "CancelledAt", "AvailableAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MemberMessages_UserId_EventKey",
                table: "MemberMessages",
                columns: new[] { "UserId", "EventKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberProfiles_Mobile",
                table: "MemberProfiles",
                column: "Mobile");

            migrationBuilder.CreateIndex(
                name: "IX_MemberProfiles_TelegramUserId",
                table: "MemberProfiles",
                column: "TelegramUserId",
                unique: true,
                filter: "[TelegramUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipPlans_CurrentVersionId",
                table: "MembershipPlans",
                column: "CurrentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipPlanVersions_PlanId",
                table: "MembershipPlanVersions",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberSubscriptions_OrderId",
                table: "MemberSubscriptions",
                column: "OrderId",
                unique: true,
                filter: "[OrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MemberSubscriptions_PlanVersionId",
                table: "MemberSubscriptions",
                column: "PlanVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberSubscriptions_UserId_EndsAt",
                table: "MemberSubscriptions",
                columns: new[] { "UserId", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentOrders_DiscountCodeId",
                table: "PaymentOrders",
                column: "DiscountCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentOrders_Gateway_ProviderReference",
                table: "PaymentOrders",
                columns: new[] { "Gateway", "ProviderReference" },
                unique: true,
                filter: "[ProviderReference] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentOrders_PlanVersionId",
                table: "PaymentOrders",
                column: "PlanVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentOrders_UserId_CreatedAt",
                table: "PaymentOrders",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReviews_OrderId",
                table: "PaymentReviews",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedOpportunities_OpportunityId",
                table: "SavedOpportunities",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedOpportunities_UserId_OpportunityId",
                table: "SavedOpportunities",
                columns: new[] { "UserId", "OpportunityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TelegramInbox_ProcessedAt_LockedUntil",
                table: "TelegramInbox",
                columns: new[] { "ProcessedAt", "LockedUntil" });

            migrationBuilder.AddForeignKey(
                name: "FK_DiscountCodes_MembershipPlans_PlanId",
                table: "DiscountCodes",
                column: "PlanId",
                principalTable: "MembershipPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MembershipPlans_MembershipPlanVersions_CurrentVersionId",
                table: "MembershipPlans",
                column: "CurrentVersionId",
                principalTable: "MembershipPlanVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            // Existing publications are historical: deployment must not alert every member.
            migrationBuilder.Sql("""
                INSERT INTO [OpportunityPublications] ([OpportunityId], [PublishedAt], [Completed])
                SELECT [Id], SYSUTCDATETIME(), CAST(1 AS bit)
                FROM [Opportunities] WHERE [Status] IN (2, 3);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [MemberProfiles]) OR EXISTS (SELECT 1 FROM [PaymentOrders])
                    THROW 51010, 'Membership data exists. Restore a reviewed backup instead of dropping account or payment history.', 1;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_MembershipPlanVersions_MembershipPlans_PlanId",
                table: "MembershipPlanVersions");

            migrationBuilder.DropTable(
                name: "MemberChallenges");

            migrationBuilder.DropTable(
                name: "MemberMessages");

            migrationBuilder.DropTable(
                name: "MemberPreferences");

            migrationBuilder.DropTable(
                name: "MembershipRuntimeStates");

            migrationBuilder.DropTable(
                name: "MemberSubscriptions");

            migrationBuilder.DropTable(
                name: "OpportunityAccessRules");

            migrationBuilder.DropTable(
                name: "OpportunityPublications");

            migrationBuilder.DropTable(
                name: "PaymentGatewaySettings");

            migrationBuilder.DropTable(
                name: "PaymentReviews");

            migrationBuilder.DropTable(
                name: "SavedOpportunities");

            migrationBuilder.DropTable(
                name: "TelegramInbox");

            migrationBuilder.DropTable(
                name: "PaymentOrders");

            migrationBuilder.DropTable(
                name: "DiscountCodes");

            migrationBuilder.DropTable(
                name: "MemberProfiles");

            migrationBuilder.DropTable(
                name: "MembershipPlans");

            migrationBuilder.DropTable(
                name: "MembershipPlanVersions");
        }
    }
}
