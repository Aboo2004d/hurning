using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HiringPs.Core.Migrations;

/// <summary>يضيف فهرسًا لسجل العمليات الموجود؛ لا يعدل أي بيانات تشغيلية.</summary>
public partial class OperationHistoryIndex : Migration
{
    /// <summary>يهيئ فهرس الترتيب والترشيح الزمني.</summary>
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.CreateIndex(
        name: "IX_Audit_At_Id", table: "Audit", columns: new[] { "At", "Id" });

    /// <summary>يزيل الفهرس فقط عند التراجع مع الحفاظ على كامل السجل.</summary>
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropIndex(
        name: "IX_Audit_At_Id", table: "Audit");
}
