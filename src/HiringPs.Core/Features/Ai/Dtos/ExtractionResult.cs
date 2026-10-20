// استخراج البيانات بالنموذج، ثم التحقق من الأدلة وتحديد النشر أو المراجعة أو الأرشفة.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

/// <summary>DTO للاستجابة المنظمة من AI؛ يميز المحتوى خارج النطاق عن قائمة الفرص المستخلصة.</summary>
public record ExtractionResult(bool InScope, List<ExtractedOpportunity> Opportunities)
{
    [JsonIgnore]
    public int? PromptVersionId { get; set; }
}
