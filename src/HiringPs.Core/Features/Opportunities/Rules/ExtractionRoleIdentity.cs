namespace HiringPs.Core;

/// <summary>يطابق الدور داخل عنصر المصدر نفسه عند تغير childKey، دون دمج أدوار متشابهة عبر مصادر مختلفة.</summary>
public static class ExtractionRoleIdentity
{
    /// <summary>يفضل المفتاح الحالي، ثم تطابقًا فريدًا في المسمى والجهة والنوع والمرجع والمكان.</summary>
    public static OpportunitySourceLink? Match(ExtractedOpportunity incoming, IReadOnlyList<OpportunitySourceLink> links,
        IReadOnlyList<ExtractedOpportunity> children, ISet<OpportunitySourceLink> consumed)
    {
        var exact = links.FirstOrDefault(x => string.Equals(x.ChildKey, incoming.ChildKey, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return consumed.Contains(exact) ? null : exact;
        // Identical role/location signatures among incoming children cannot share one historical identity.
        if (children.Any(x => !string.Equals(x.ChildKey, incoming.ChildKey, StringComparison.OrdinalIgnoreCase) && SameSignature(x, incoming))) return null;
        var matches = links.Where(x => !consumed.Contains(x) && SameRole(incoming, x.Opportunity)).ToList();
        return matches.Select(x => x.OpportunityId).Distinct().Count() == 1 ? matches.FirstOrDefault() : null;
    }

    /// <summary>يقارن هوية دور مع سجل محفوظ مع إبقاء الاختلاف الجوهري في التخصص أو المستوى أو الموقع.</summary>
    public static bool SameRole(ExtractedOpportunity incoming, Opportunity stored) =>
        incoming.Kind == stored.Kind && !string.IsNullOrWhiteSpace(incoming.Title) && !string.IsNullOrWhiteSpace(incoming.Organization)
        && LocationSearch.Normalize(incoming.Title) == LocationSearch.Normalize(stored.Title)
        && TextRules.OrganizationKey(incoming.Organization) == TextRules.OrganizationKey(stored.Organization)
        && (incoming.ReferenceNumber.Length == 0 || stored.ReferenceNumber.Length == 0
            || DuplicateMatcher.ReferenceKey(incoming.ReferenceNumber) == DuplicateMatcher.ReferenceKey(stored.ReferenceNumber))
        && ExplicitWorkLocationRules.Compatible(incoming.Locations, stored.Locations);

    /// <summary>يرفض المطابقة البديلة عندما تتكرر هوية الدور في الرد نفسه.</summary>
    private static bool SameSignature(ExtractedOpportunity a, ExtractedOpportunity b) =>
        a.Kind == b.Kind && LocationSearch.Normalize(a.Title) == LocationSearch.Normalize(b.Title)
        && TextRules.OrganizationKey(a.Organization) == TextRules.OrganizationKey(b.Organization)
        && ExplicitWorkLocationRules.Compatible(a.Locations, b.Locations)
        && (a.ReferenceNumber.Length == 0 || b.ReferenceNumber.Length == 0 || DuplicateMatcher.ReferenceKey(a.ReferenceNumber) == DuplicateMatcher.ReferenceKey(b.ReferenceNumber));
}
