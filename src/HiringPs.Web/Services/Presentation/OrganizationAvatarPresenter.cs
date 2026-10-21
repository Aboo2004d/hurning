namespace HiringPs.Web.Models;

/// <summary>يحافظ على لون وحرف شارة الجهة المعتمدين، بدل تكرار حسابهما داخل Razor.</summary>
public static class OrganizationAvatarPresenter
{
    /// <summary>يختار أول حرف أو رقم، ولونًا ثابتًا من ستة ألوان بناءً على الاسم، مع بديل للاسم الفارغ.</summary>
    public static OrganizationAvatarDto Create(string? organization)
    {
        var tone = 0;
        var initial = default(char);
        foreach (var character in organization ?? "")
        {
            tone = (tone + character) % 6;
            if (initial == default && char.IsLetterOrDigit(character))
                initial = character;
        }
        return new(tone, initial == default ? "•" : char.ToUpperInvariant(initial).ToString());
    }
}
