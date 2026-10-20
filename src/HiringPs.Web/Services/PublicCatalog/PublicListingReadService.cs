using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Services.PublicCatalog;

/// <summary>استعلامات القوائم العامة وRSS؛ لا يعيد كيانات الفرص أو المصادر إلى Controllers.</summary>
public sealed class PublicListingReadService(HiringDb db, HiringPs.Web.Models.PublicCatalog catalog)
{
    /// <summary>يطبق بحث الرئيسية وترتيبها وترقيمها ويحافظ على شروط ظهور قسم الإغلاق دون ربطه بالفلاتر.</summary>
    public async Task<PublicListingResultDto?> ReadAsync(
        PublicListingFilterDto input, bool archive, string sort, int page, DateOnly today, CancellationToken ct)
    {
        var filters = await catalog.FiltersAsync(input.Kind, archive, ct);
        var scoped = PublicListingFilters.Scope(db.Opportunities.AsNoTracking(), archive, today);
        var query = PublicListingFilters.Apply(scoped, input, filters);
        var result = await PageAsync(Order(query, sort), page, requireResults: false, ct);
        if (result is null)
            return null;
        List<OpportunityCardDto> closing = [];
        var hasFilters = !string.IsNullOrEmpty(input.Query) || !string.IsNullOrEmpty(input.City)
            || !string.IsNullOrEmpty(input.Category) || !string.IsNullOrEmpty(input.Employment)
            || !string.IsNullOrEmpty(input.Experience);
        if (!archive && (input.Kind is null || input.Kind == OpportunityKind.Job)
            && !hasFilters && page == 1 && sort == ListingSort.Newest)
        {
            closing = await scoped
                .Where(x => x.Kind == OpportunityKind.Job && x.Deadline != null && x.Deadline <= today.AddDays(7))
                .OrderBy(x => x.Deadline).ThenByDescending(x => x.SortDate).Take(4)
                .Select(PublicOpportunityProjection.Card).ToListAsync(ct);
        }
        return new(result, filters, closing);
    }

    /// <summary>يقرأ صفحة مدينة حالية؛ الصفحة الفارغة أو غير الموجودة تعيد null كما في مسار التصفح السابق.</summary>
    public Task<OpportunityPageDto?> CityAsync(OpportunityKind kind, string searchKey, int page, DateOnly today, CancellationToken ct) =>
        PageAsync(Order(db.Opportunities.AsNoTracking().Current(today)
            .Where(x => x.Kind == kind && x.LocationSearchText.Contains(searchKey)), ListingSort.Newest), page, true, ct);

    /// <summary>يقرأ فرص مفاتيح الجهة الحالية دون تغيير تجميع أسماء الجهات أو صلاحية التفاصيل.</summary>
    public Task<OpportunityPageDto?> OrganizationAsync(string[] keys, int page, DateOnly today, CancellationToken ct) =>
        PageAsync(Order(db.Opportunities.AsNoTracking().Current(today)
            .Where(x => keys.Contains(x.NormalizedOrganization)), ListingSort.Newest), page, true, ct);

    /// <summary>يستخدم فلاتر الرئيسية نفسها لخلاصة عامة، مع شرط إتاحة التفاصيل للعامة قبل أخذ الحد الأقصى.</summary>
    public async Task<List<OpportunityCardDto>> FeedAsync(
        PublicListingFilterDto input, bool archive, string sort, DateOnly today, int limit, CancellationToken ct)
    {
        var filters = await catalog.FiltersAsync(input.Kind, archive, ct);
        var query = PublicListingFilters.Scope(db.Opportunities.AsNoTracking(), archive, today)
            .Where(x => db.OpportunityAccessRules.Any(rule => rule.Kind == x.Kind && rule.PublicDetails));
        query = PublicListingFilters.Apply(query, input, filters);
        return await Order(query, sort).Take(limit).Select(PublicOpportunityProjection.Card).ToListAsync(ct);
    }

    /// <summary>يطبق ترتيب الإغلاق أو الأحدث مع كاسر التعادل بمعرّف الفرصة كما في القائمة الحالية.</summary>
    private static IOrderedQueryable<Opportunity> Order(IQueryable<Opportunity> query, string sort) =>
        sort == ListingSort.Closing
            ? query.OrderBy(x => x.Deadline == null).ThenBy(x => x.Deadline)
                .ThenByDescending(x => x.SortDate).ThenByDescending(x => x.Id)
            : query.OrderByDescending(x => x.SortDate).ThenByDescending(x => x.Id);

    /// <summary>يتحقق من رقم الصفحة قبل تنفيذ إسقاط البطاقات؛ الرئيسية وحدها تسمح بصفحة أولى فارغة.</summary>
    private static async Task<OpportunityPageDto?> PageAsync(
        IOrderedQueryable<Opportunity> query, int page, bool requireResults, CancellationToken ct)
    {
        var count = await query.CountAsync(ct);
        if ((requireResults && count == 0) || page < 1 || page > PublicListing.PageCount(count))
            return null;
        var items = await query.Skip((page - 1) * PublicListing.PageSize).Take(PublicListing.PageSize)
            .Select(PublicOpportunityProjection.Card).ToListAsync(ct);
        return new(items, count);
    }
}
