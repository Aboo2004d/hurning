// تسميات معروفة لقيم التصنيف ونوع التوظيف والخبرة المستخرجة من المصادر، مع الرجوع للنص الأصلي.
using System.Globalization;
using System.Text.RegularExpressions;
using HiringPs.Core;

namespace HiringPs.Web.Models;

/// <summary>مكون FilterOption ضمن مجال Opportunities؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public sealed record FilterOption(string Value, string Label);
