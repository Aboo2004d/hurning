using HiringPs.Core;

namespace HiringPs.Web.Models;

/// <summary>مرشحات سجل العمليات وصفحته الحالية، دون كيانات قاعدة بيانات.</summary>
public sealed record OperationLogPage(OperationFilterDto Filter, AdminPage<OperationSummaryDto> Events);
