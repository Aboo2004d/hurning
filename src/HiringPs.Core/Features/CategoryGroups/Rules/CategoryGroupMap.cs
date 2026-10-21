namespace HiringPs.Core;

/// <summary>لقطة المجموعات المعتمدة؛ التشابه الإملائي لا يدخل في الاستحقاق أو حساب الحد.</summary>
public sealed class CategoryGroupMap(IEnumerable<(string Label, int GroupId, string GroupName)> entries)
{
    private readonly IReadOnlyDictionary<string, (int GroupId, string GroupName)> memberships = entries
        .ToDictionary(x => Key(x.Label), x => (x.GroupId, x.GroupName), StringComparer.Ordinal);
    public static CategoryGroupMap Empty { get; } = new([]);

    /// <summary>مفتاح اسم تام مع حماية المسميات الطويلة من حدود الفهارس.</summary>
    public static string Key(string? label)
    {
        var key = CategoryRules.Key(label);
        return key.Length <= 190 ? "text:" + key : "sha256:" + TextRules.Hash(key);
    }
    /// <summary>يحسب المجموعة وحدة واحدة؛ يبقي الاسم غير المصنف مستقلاً.</summary>
    public string Identity(string? label) => memberships.TryGetValue(Key(label), out var group)
        ? "group:" + group.GroupId : "label:" + Key(label);
    /// <summary>يطابق الاسم نفسه أو اسمين داخل نفس المجموعة المعتمدة فقط.</summary>
    public bool Matches(string? left, string? right) => !string.IsNullOrWhiteSpace(left)
        && !string.IsNullOrWhiteSpace(right) && Identity(left) == Identity(right);
    /// <summary>اسم المجموعة للعرض دون استبدال الاسم الأصلي في البيانات.</summary>
    public string? GroupName(string label) => memberships.GetValueOrDefault(Key(label)).GroupName;
}
