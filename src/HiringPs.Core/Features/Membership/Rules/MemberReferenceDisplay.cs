using System.Text.RegularExpressions;

namespace HiringPs.Core;

/// <summary>يعرض أسماء الأعضاء المشار إليهم في نصوص التدقيق دون تغيير السجل الأصلي.</summary>
public static class MemberReferenceDisplay
{
    private static readonly Regex References = new(@"\bMember\s+(?<id>[^\s;]+)(?=;|$)", RegexOptions.CultureInvariant);

    /// <summary>يستخرج معرّفات مراجع الأعضاء فقط، دون معرّفات الطلبات أو المعاملات.</summary>
    public static IEnumerable<string> Ids(string text) => References.Matches(text).Select(x => x.Groups["id"].Value);

    /// <summary>يستبدل مرجع العضو باسمه للعرض؛ يظل النص المخزن ومعرّفات المعاملات كما هو.</summary>
    public static string Render(string text, IReadOnlyDictionary<string, string> names) =>
        References.Replace(text, match => "Member " + names.GetValueOrDefault(match.Groups["id"].Value, "—"));
}
