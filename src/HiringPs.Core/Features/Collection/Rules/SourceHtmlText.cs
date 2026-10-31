using System.Text.RegularExpressions;
using AngleSharp.Dom;

namespace HiringPs.Core;

/// <summary>يحفظ حدود فقرات وحقول HTML بدل دمج آخر كلمة من الحقل بأول كلمة من التالي.</summary>
public static class SourceHtmlText
{
    /// <summary>يضيف فواصل نصية لعناصر التخطيط ثم يوحد المسافات دون تغيير الكلمات الأصلية.</summary>
    public static string Read(IDocument document)
    {
        foreach (var node in document.QuerySelectorAll("p,div,section,article,h1,h2,h3,h4,h5,h6,li,dt,dd,th,td,tr,br"))
        {
            node.Prepend(document.CreateTextNode("\n"));
            node.Append(document.CreateTextNode("\n"));
        }
        var text = Regex.Replace(document.Body?.TextContent ?? "", @"[^\S\r\n]+", " ");
        return Regex.Replace(text, @"\s*\r?\n\s*", "\n").Trim();
    }
}
