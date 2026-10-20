// طلب Gemini وقراءة ردّه: حدود المدخلات والمخرجات، مخطط JSON، وتصنيف أخطاء المزوّد.
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

/// <summary>مكون AiTransientException ضمن مجال Ai؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public class AiTransientException(string message) : Exception(message);
