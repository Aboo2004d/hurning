using HiringPs.Core;

namespace HiringPs.Web.Models;

/// <summary>مرشحات الأخطاء وصفحة النتائج دون كشف كيانات التخزين للواجهة.</summary>
public sealed record SystemErrorsPage(SystemErrorFilterDto Filter, AdminPage<SystemErrorSummaryDto> Errors);
