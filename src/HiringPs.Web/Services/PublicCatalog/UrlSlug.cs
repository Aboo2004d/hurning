// قواعد مشتركة للصفحات العامة: حجم الصفحة، الفرص المنشورة الحالية، روابط التفاصيل والـslug.
using System.Globalization;
using System.Text;
using HiringPs.Core;

namespace HiringPs.Web.Models;

/// <summary>مكون UrlSlug ضمن مجال Services؛ يحافظ على عقد وسلوك تحديث المنصة.</summary>
public static class UrlSlug
{
    /// <summary>ينفذ Create وفق قواعد UrlSlug الحالية دون تغيير عقد المستدعين.</summary>
    public static string Create(string? text, int maxLength = 60)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";
        var value = text.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        var builder = new StringBuilder(value.Length);
        var dash = false;
        foreach (var c in value)
        {
            var category = char.GetUnicodeCategory(c);
            if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.Format || c == 'ـ')
                continue;
            if (char.IsLetterOrDigit(c))
            {
                if (dash && builder.Length > 0)
                    builder.Append('-');
                builder.Append(c);
                dash = false;
            }
            else
                dash = true;
        }
        var slug = builder.ToString();
        if (slug.Length <= maxLength)
            return slug;
        var cut = slug.LastIndexOf('-', maxLength);
        return (cut > maxLength / 2 ? slug[..cut] : slug[..maxLength]).Trim('-');
    }
}
