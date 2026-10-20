using Microsoft.Extensions.Configuration;

namespace HiringPs.Web.Infrastructure;

// Private files are served only through owner/admin endpoints, never static files.
/// <summary>يحفظ أدلة الدفع كملفات خاصة، ولا يتيح الوصول إليها إلا عبر مسارات المالك أو الإدارة.</summary>
public sealed class PaymentEvidenceStore(IConfiguration configuration)
{
    private string DirectoryPath =>
        Path.GetFullPath(
            configuration["Membership:EvidenceDirectory"] ?? "data/payment-evidence",
            AppContext.BaseDirectory
        );

    /// <summary>يتحقق من حجم وصيغة الصورة ويحفظها باسم مولد؛ لا يثق باسم الملف الذي أرسله المتصفح.</summary>
    public async Task<string?> SaveAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return null;
        if (file.Length > 5 * 1024 * 1024)
            throw new InvalidOperationException("EvidenceInvalid");
        using var memory = new MemoryStream();
        await file.CopyToAsync(memory, ct);
        var bytes = memory.ToArray();
        var png =
            bytes.Length >= 8
            && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var jpeg = bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255;
        if (!png && !jpeg)
            throw new InvalidOperationException("EvidenceInvalid");
        Directory.CreateDirectory(DirectoryPath);
        var name = Guid.NewGuid().ToString("N") + (png ? ".png" : ".jpg");
        await File.WriteAllBytesAsync(Path.Combine(DirectoryPath, name), bytes, ct);
        return name;
    }

    /// <summary>يحل اسم دليل مولد داخل المجلد الخاص فقط ويرفض أي مسار أو امتداد غير مسموح.</summary>
    public string PathFor(string name)
    {
        if (
            name != Path.GetFileName(name)
            || name.Length != 36
            || !Guid.TryParseExact(name[..32], "N", out _)
            || Path.GetExtension(name) is not (".png" or ".jpg")
        )
            throw new InvalidOperationException("EvidenceInvalid");
        return Path.Combine(DirectoryPath, name);
    }
}
