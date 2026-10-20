using System.Text.RegularExpressions;

namespace HiringPs.Core;

/// <summary>DTO للمواعيد المقبولة وملاحظات رفض الأدلة عند مراجعة نتائج الاستخراج.</summary>
public record CheckedDates(IReadOnlyList<ExtractedDate> Dates, IReadOnlyList<string> Errors);
