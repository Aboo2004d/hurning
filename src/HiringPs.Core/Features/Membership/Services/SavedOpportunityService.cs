using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>يقرأ محفوظات العضو ويعدل روابط الحفظ مع فرض الملكية في كل استعلام.</summary>
public sealed class SavedOpportunityService(HiringDb db)
{
    /// <summary>يسقط المحفوظات قبل الترقيم دون تحميل الرسم الكامل للفرصة.</summary>
    public IQueryable<SavedOpportunitySummaryDto> ForMember(string owner) => db.SavedOpportunities.AsNoTracking()
        .Where(x => x.UserId == owner).OrderByDescending(x => x.Id)
        .Select(x => new SavedOpportunitySummaryDto
        {
            Id = x.Id,
            OpportunityId = x.OpportunityId,
            SavedAt = x.SavedAt,
            Kind = x.Opportunity.Kind,
            Title = x.Opportunity.Title,
            Organization = x.Opportunity.Organization,
            Locations = x.Opportunity.Locations,
            Status = x.Opportunity.Status,
        });

    /// <summary>يقرأ حالات حفظ الفرص الظاهرة في الصفحة فقط.</summary>
    public async Task<HashSet<long>> IdsAsync(string owner, long[] ids, CancellationToken ct) =>
        (await db.SavedOpportunities.AsNoTracking().Where(x => x.UserId == owner && ids.Contains(x.OpportunityId))
            .Select(x => x.OpportunityId).ToListAsync(ct)).ToHashSet();

    /// <summary>يفحص حفظ فرصة واحدة لدى صاحب الجلسة.</summary>
    public Task<bool> ContainsAsync(string owner, long id, CancellationToken ct) =>
        db.SavedOpportunities.AsNoTracking().AnyAsync(x => x.UserId == owner && x.OpportunityId == id, ct);

    /// <summary>يحسب محفوظات العضو فقط دون تسليم سجلاتها إلى لوحة الحساب.</summary>
    public Task<int> CountAsync(string owner, CancellationToken ct) =>
        db.SavedOpportunities.CountAsync(x => x.UserId == owner, ct);

    /// <summary>يحفظ فرصة ظاهرة مرة واحدة ويتحمل سباق الحفظ من نافذتين بنفس سياسة المفتاح الفريد السابقة.</summary>
    public async Task<bool> AddAsync(string owner, long id, CancellationToken ct)
    {
        if (!await db.Opportunities.AnyAsync(x => x.Id == id
            && (x.Status == PublicationStatus.Published || x.Status == PublicationStatus.Archived), ct))
            return false;
        if (!await db.SavedOpportunities.AnyAsync(x => x.UserId == owner && x.OpportunityId == id, ct))
        {
            var saved = new SavedOpportunity { UserId = owner, OpportunityId = id };
            db.SavedOpportunities.Add(saved);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
            {
                db.Entry(saved).State = EntityState.Detached;
                if (!await db.SavedOpportunities.AnyAsync(x => x.UserId == owner && x.OpportunityId == id, ct))
                    throw;
            }
        }
        return true;
    }

    /// <summary>يزيل الحفظ بمعرّف الفرصة، كما تفعل أزرار البطاقات.</summary>
    public Task<int> UnsaveAsync(string owner, long opportunityId, CancellationToken ct) =>
        db.SavedOpportunities.Where(x => x.UserId == owner && x.OpportunityId == opportunityId).ExecuteDeleteAsync(ct);

    /// <summary>يزيل صف حفظ مملوكًا للعضو بمعرّف الصف، كما تفعل قائمة المحفوظات.</summary>
    public Task<int> RemoveAsync(string owner, long savedId, CancellationToken ct) =>
        db.SavedOpportunities.Where(x => x.UserId == owner && x.Id == savedId).ExecuteDeleteAsync(ct);
}
