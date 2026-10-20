using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>قراءة نسخ القوالب واعتماد نسخة جديدة مع قفل SQL يمنع اعتماد تعديلين متنافسين.</summary>
public sealed class SocialTemplateManagementService(HiringDb db)
{
    /// <summary>يتحقق من منصة ونطاق القالب المعتمدين.</summary>
    public static bool ValidScope(string provider, string scope) =>
        (provider is "Facebook" or "LinkedIn")
        && (scope == "general" || Enum.GetNames<OpportunityKind>().Contains(scope));

    /// <summary>يسقط النسخ إلى حقول السجل والمحرر دون تتبع.</summary>
    public IQueryable<SocialTemplateSummaryDto> Versions() => db.SocialTemplateVersions.AsNoTracking()
        .Select(x => new SocialTemplateSummaryDto
        {
            Id = x.Id, Provider = x.Provider, Scope = x.Scope, Number = x.Number,
            CreatedAt = x.CreatedAt, Actor = x.Actor, Body = x.Body, Active = x.Active,
        });

    /// <summary>يعتمد نسخة جديدة بعد مقارنة النسخة النشطة داخل نفس المعاملة.</summary>
    public async Task<SocialTemplateSaveOutcome> SaveAsync(SocialTemplateSaveCommandDto input, CancellationToken ct)
    {
        if (!ValidScope(input.Provider, input.Scope) || !SocialTemplateText.Valid(input.Body))
            return SocialTemplateSaveOutcome.InvalidRequest;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var resource = "SocialTemplate:" + input.Provider + ":" + input.Scope;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"DECLARE @r int; EXEC @r=sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r < 0 THROW 51000, 'Template busy', 1;",
            ct
        );
        var active = await db.SocialTemplateVersions.SingleOrDefaultAsync(
            x => x.Provider == input.Provider && x.Scope == input.Scope && x.Active,
            ct
        );
        if ((active?.Id ?? 0) != input.ExpectedActiveId)
        {
            return SocialTemplateSaveOutcome.Conflict;
        }
        var number =
            (
                await db
                    .SocialTemplateVersions.Where(x =>
                        x.Provider == input.Provider && x.Scope == input.Scope
                    )
                    .MaxAsync(x => (int?)x.Number, ct)
                ?? 0
            ) + 1;
        if (active is not null)
        {
            active.Active = false;
            await db.SaveChangesAsync(ct);
        }
        db.SocialTemplateVersions.Add(
            new SocialTemplateVersion
            {
                Provider = input.Provider,
                Scope = input.Scope,
                Number = number,
                Active = true,
                Body = input.Body.Trim(),
                Actor = input.Actor,
            }
        );
        db.Audit.Add(
            new AuditEntry
            {
                Actor = input.Actor,
                Action = "SocialTemplate.Version",
                Detail = input.Provider + ":" + input.Scope + ":" + number,
            }
        );
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return SocialTemplateSaveOutcome.Saved;
    }
}
