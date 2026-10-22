using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>يحل بريد العضو إلى هويته الداخلية وفق تطبيع Identity، دون اختيار حساب عند التباس البريد.</summary>
public sealed class AdminMemberLookupService(HiringDb db)
{
    /// <summary>يعيد حسابًا واحدًا له ملف عضو فقط؛ البريد غير الصحيح أو المكرر لا يمنح أي صلاحية.</summary>
    public async Task<AdminMemberIdentityDto?> ByEmailAsync(string? email, CancellationToken ct)
    {
        email = email?.Trim();
        if (string.IsNullOrEmpty(email) || email.Length > 256 || !new EmailAddressAttribute().IsValid(email)) return null;
        var normalized = new UpperInvariantLookupNormalizer().NormalizeEmail(email);
        var matches = await (from user in db.Users.AsNoTracking()
            join member in db.MemberProfiles.AsNoTracking() on user.Id equals member.UserId
            where user.NormalizedEmail == normalized
            select new AdminMemberIdentityDto(user.Id, member.Name, user.Email ?? ""))
            .Take(2).ToListAsync(ct);
        return matches.Count == 1 ? matches[0] : null;
    }

    /// <summary>يعيد البريد الحقيقي للتنقل بعد إجراء إداري يحتفظ بالمعرّف داخليًا.</summary>
    public Task<string?> EmailAsync(string userId, CancellationToken ct) => db.Users.AsNoTracking()
        .Where(x => x.Id == userId).Select(x => x.Email).SingleOrDefaultAsync(ct);
    /// <summary>يقرأ أسماء مراجع التدقيق الحالية باستعلام واحد للصفحة دون تعديل التاريخ المخزن.</summary>
    public async Task<IReadOnlyDictionary<string, string>> ReferencedNamesAsync(IEnumerable<string> details, CancellationToken ct)
    {
        var ids = details.SelectMany(MemberReferenceDisplay.Ids).Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<string, string>();
        var names = await db.MemberProfiles.AsNoTracking().Where(m => ids.Contains(m.UserId))
            .Select(m => new { m.UserId, m.Name }).ToDictionaryAsync(m => m.UserId, m => m.Name, ct);
        return names;
    }
}
