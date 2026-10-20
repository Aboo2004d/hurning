using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>استعلامات إدارة الأعضاء تسقط حقول العرض وتحافظ على حدود بحث الاشتراكات وبيئتها.</summary>
public sealed class AdminMemberReadService(HiringDb db)
{
    /// <summary>يطبق مرشحات الاسم والبريد والهاتف والإيقاف والربط والباقة قبل الترقيم.</summary>
    public IQueryable<AdminMemberSummaryDto> Members(string? q, bool? suspended, bool? linked, int? planId)
    {
        var query = db.MemberProfiles.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(x =>
                x.Name.Contains(q)
                || x.Mobile.Contains(q)
                || db.Users.Any(u => u.Id == x.UserId && u.Email!.Contains(q))
            );
        if (suspended is not null)
            query = query.Where(x => x.Suspended == suspended);
        if (linked is not null)
            query = query.Where(x => (x.TelegramUserId != null) == linked);
        if (planId is not null)
            query = query.Where(x =>
                db.MemberSubscriptions.Any(s =>
                    s.UserId == x.UserId
                    && !s.Cancelled
                    && s.StartsAt <= DateTimeOffset.UtcNow
                    && s.EndsAt > DateTimeOffset.UtcNow
                    && s.PlanVersion.PlanId == planId
                )
            );
        return query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.UserId)
            .Select(x => new AdminMemberSummaryDto
            {
                UserId = x.UserId, Name = x.Name, Mobile = x.Mobile,
                Activated = x.Activated, Suspended = x.Suspended, SuspensionReason = x.SuspensionReason,
            });
    }

    /// <summary>يقرأ حقول العضو اللازمة لشاشة التفاصيل.</summary>
    public Task<AdminMemberSummaryDto?> MemberAsync(string id, CancellationToken ct) =>
        Members(null, null, null, null).SingleOrDefaultAsync(x => x.UserId == id, ct);

    /// <summary>يقرأ بريد حساب Identity المرتبط بالعضو.</summary>
    public Task<string?> EmailAsync(string id, CancellationToken ct) =>
        db.Users.Where(x => x.Id == id).Select(x => x.Email).SingleAsync(ct);

    /// <summary>يعيد السجل المرتبط بمعرّف العضو وفق البحث السابق دون تغيير مدى السجل المعروض.</summary>
    public IQueryable<AdminAuditSummaryDto> Audit(string id) => db.Audit.AsNoTracking()
        .Where(x => x.Detail.Contains(id)).OrderByDescending(x => x.At).ThenByDescending(x => x.Id)
        .Select(x => new AdminAuditSummaryDto(x.At, x.Actor, x.Action, x.Detail));
}
