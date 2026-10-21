using System.Globalization;
using System.Text;

namespace HiringPs.Core;

/// <summary>اقتراحات للمسؤول فقط: صيغ معروفة وتشابه إملائي أعلى من 80 بالمئة، دون التأثير على التنبيهات.</summary>
public static class CategorySimilarityRules
{
    public const double MinimumSimilarity = .80;
    private static readonly IReadOnlyDictionary<string, string> Known = BuildAliases();

    /// <summary>لا يوسع الاختيارات الفارغة ولا يقبل مجرد احتواء اسم؛ يمنع دمج المجالات المعروفة المختلفة.</summary>
    public static bool Suggests(string? preference, string? opportunity)
    {
        var left = Normalize(preference);
        var right = Normalize(opportunity);
        if (left.Length == 0 || right.Length == 0) return false;
        if (left == right) return true;
        var leftKnown = Known.GetValueOrDefault(left);
        var rightKnown = Known.GetValueOrDefault(right);
        if (leftKnown is not null && rightKnown is not null) return leftKnown == rightKnown;
        if (left.Length < 5 || right.Length < 5 || left.Length > 200 || right.Length > 200) return false;
        if (Similarity(left, right) <= MinimumSimilarity) return false;
        // A shared long phrase must not hide a changed specialty or an added qualifier.
        var leftWords = left.Split(' ').OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var rightWords = right.Split(' ').OrderBy(x => x, StringComparer.Ordinal).ToArray();
        if (leftWords.Length != rightWords.Length) return false;
        for (var i = 0; i < leftWords.Length; i++)
        {
            if (leftWords[i] == rightWords[i]) continue;
            var a = Known.GetValueOrDefault(leftWords[i]);
            var b = Known.GetValueOrDefault(rightWords[i]);
            if (a is not null && b is not null)
            {
                if (a != b) return false;
                continue;
            }
            if (leftWords[i].Length < 5 || rightWords[i].Length < 5
                || Similarity(leftWords[i], rightWords[i]) <= MinimumSimilarity) return false;
        }
        return true;
    }

    /// <summary>يميز الصيغ المعروفة لشرح الاقتراح دون تقديمها كنسبة تشابه حروف.</summary>
    public static bool KnownEquivalent(string left, string right)
    {
        var a = Known.GetValueOrDefault(Normalize(left));
        return a is not null && a == Known.GetValueOrDefault(Normalize(right));
    }

    /// <summary>ينظف اختلاف حالة الأحرف والتشكيل والتطويل وأداة التعريف؛ لا يغير الأسماء المحفوظة.</summary>
    public static string Normalize(string? value)
    {
        var normalized = TextRules.Normalize(value ?? "").Normalize(NormalizationForm.FormKC);
        var buffer = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (character == '\u0640' || category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark) continue;
            buffer.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }
        var words = buffer.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Length > 4 && word.StartsWith("ال", StringComparison.Ordinal) ? word[2..] : word)
            .ToArray();
        if (words.Length > 1 && words[0] == "the") words = words[1..];
        return string.Join(' ', words);
    }

    /// <summary>نسبة تشابه بمسافة التحرير مع قلب حرفين متجاورين؛ ليست إثباتًا لتساوي المعنى.</summary>
    public static double Similarity(string left, string right)
    {
        if (left.Length == 0 || right.Length == 0) return 0;
        if (left == right) return 1;
        if (left.Length > 200 || right.Length > 200) return 0;
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        int[]? beforePrevious = null;
        for (var i = 1; i <= left.Length; i++)
        {
            var current = new int[right.Length + 1];
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1));
                if (i > 1 && j > 1 && left[i - 1] == right[j - 2] && left[i - 2] == right[j - 1])
                    current[j] = Math.Min(current[j], beforePrevious![j - 2] + 1);
            }
            beforePrevious = previous;
            previous = current;
        }
        return 1d - (double)previous[right.Length] / Math.Max(left.Length, right.Length);
    }

    private static IReadOnlyDictionary<string, string> BuildAliases()
    {
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in CategoryDefinitions.All)
            foreach (var alias in entry.Aliases.Append(entry.Arabic).Append(entry.English))
                aliases[Normalize(alias)] = entry.Key;
        return aliases;
    }
}
