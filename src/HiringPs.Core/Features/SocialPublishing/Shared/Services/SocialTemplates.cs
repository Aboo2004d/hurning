using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>يختار نسخة قالب النشر الفعالة للمنصة ونوع الفرصة مع الرجوع للقالب العام.</summary>
public sealed class SocialTemplates(HiringDb db)
{
    /// <summary>يختار القالب النشط لنوع الفرصة مع الرجوع للقالب العام ومشاركة LinkedIn بين الوجهتين.</summary>
    public async Task<SocialTemplateVersion> ResolveAsync(
        string provider,
        OpportunityKind kind,
        CancellationToken ct
    )
    {
        provider = SocialPublishing.TemplateProvider(provider);
        var scope = kind.ToString();
        return await db
                .SocialTemplateVersions.AsNoTracking()
                .Where(x =>
                    x.Provider == provider && x.Active && (x.Scope == scope || x.Scope == "general")
                )
                .OrderByDescending(x => x.Scope == scope)
                .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("SocialTemplateMissing");
    }
}
