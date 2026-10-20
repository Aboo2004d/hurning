namespace HiringPs.Core;

/// <summary>روابط HTTPS للمرفقات المحفوظة؛ لا يجلب الملف أو يتصل بمصدره.</summary>
public static class PublicAttachmentLinks
{
    /// <summary>يعيد روابط فريدة، أو قائمة فارغة إذا كان JSON التاريخي غير صالح.</summary>
    public static List<string> Read(string? json)
    {
        try
        {
            return (System.Text.Json.JsonSerializer.Deserialize<string[]>(json ?? "[]") ?? [])
                .Where(file =>
                    Uri.TryCreate(file, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
                )
                .Distinct()
                .ToList();
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }

}
