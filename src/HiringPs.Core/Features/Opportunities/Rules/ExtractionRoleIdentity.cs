namespace HiringPs.Core;

/// <summary>يطابق الدور داخل عنصر المصدر نفسه عند تغير childKey، دون دمج أدوار متشابهة عبر مصادر مختلفة.</summary>
public static class ExtractionRoleIdentity
{
    /// <summary>يفضل المفتاح الحالي، ثم تطابقًا فريدًا في المسمى والجهة والنوع والمرجع والمكان.</summary>
    public static OpportunitySourceLink? Match(ExtractedOpportunity incoming, IReadOnlyList<OpportunitySourceLink> links,
        IReadOnlyList<ExtractedOpportunity> children, ISet<OpportunitySourceLink> consumed, string source = "")
    {
        var exact = links.FirstOrDefault(x => string.Equals(x.ChildKey, incoming.ChildKey, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return consumed.Contains(exact) ? null : exact;
        if (incoming.ReferenceNumber.Length > 0
            && children.Count(x => DuplicateMatcher.ReferenceKey(x.ReferenceNumber) == DuplicateMatcher.ReferenceKey(incoming.ReferenceNumber)) == 1)
        {
            var byReference = links.Where(x => !consumed.Contains(x) && x.Opportunity.Kind == incoming.Kind
                && TextRules.OrganizationKey(x.Opportunity.Organization) == TextRules.OrganizationKey(incoming.Organization)
                && x.Opportunity.ReferenceNumber.Length > 0
                && DuplicateMatcher.ReferenceKey(x.Opportunity.ReferenceNumber) == DuplicateMatcher.ReferenceKey(incoming.ReferenceNumber)).ToList();
            if (byReference.Select(x => x.OpportunityId).Distinct().Count() == 1) return byReference[0];
        }
        // Identical role/location signatures among incoming children cannot share one historical identity.
        if (children.Any(x => !string.Equals(x.ChildKey, incoming.ChildKey, StringComparison.OrdinalIgnoreCase) && SameSignature(x, incoming))) return null;
        var matches = links.Where(x => !consumed.Contains(x) && SameSourceRole(incoming, x.Opportunity, source, children.Count == 1)).ToList();
        return matches.Select(x => x.OpportunityId).Distinct().Count() == 1 ? matches.FirstOrDefault() : null;
    }

    /// <summary>يقارن هوية دور مع سجل محفوظ مع إبقاء الاختلاف الجوهري في التخصص أو المستوى أو الموقع.</summary>
    public static bool SameRole(ExtractedOpportunity incoming, Opportunity stored) =>
        incoming.Kind == stored.Kind && !string.IsNullOrWhiteSpace(incoming.Title) && !string.IsNullOrWhiteSpace(incoming.Organization)
        && TitleKey(incoming.Title) == TitleKey(stored.Title)
        && TextRules.OrganizationKey(incoming.Organization) == TextRules.OrganizationKey(stored.Organization)
        && (incoming.ReferenceNumber.Length == 0 || stored.ReferenceNumber.Length == 0
            || DuplicateMatcher.ReferenceKey(incoming.ReferenceNumber) == DuplicateMatcher.ReferenceKey(stored.ReferenceNumber))
        && ExplicitWorkLocationRules.Compatible(incoming.Locations, stored.Locations);

    /// <summary>يتعرف إلى توسعة عنوان عام قديم فقط عندما يؤكد عنوان المصدر الأصلي والدور المنفرد والجهة ذلك.</summary>
    public static bool SameSourceRole(ExtractedOpportunity incoming, Opportunity stored, string source, bool singleRole)
    {
        if (SameRole(incoming, stored)) return true;
        if (!singleRole || incoming.Kind != stored.Kind || source.Length == 0
            || !ExplicitWorkLocationRules.Compatible(incoming.Locations, stored.Locations)
            || incoming.ReferenceNumber.Length > 0 && stored.ReferenceNumber.Length > 0
                && DuplicateMatcher.ReferenceKey(incoming.ReferenceNumber) != DuplicateMatcher.ReferenceKey(stored.ReferenceNumber)) return false;
        var heading = SourceEvidence.PrimaryPalCareerTitle(source);
        var oldTitle = TitleKey(stored.Title);
        if (heading.Length == 0 || TitleKey(heading) != oldTitle
            || !(" " + TitleKey(incoming.Title) + " ").Contains(" " + oldTitle + " ", StringComparison.Ordinal)
            || !TextEvidence.TitleContains(source, incoming.Title)) return false;
        var oldOrg = " " + LocationSearch.Normalize(stored.Organization) + " ";
        var newOrg = " " + LocationSearch.Normalize(incoming.Organization) + " ";
        return oldOrg.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2
            && (oldOrg == newOrg || oldOrg.Contains(newOrg, StringComparison.Ordinal) || newOrg.Contains(oldOrg, StringComparison.Ordinal))
            && TextEvidence.OrganizationContains(source, stored.Organization)
            && TextEvidence.OrganizationContains(source, incoming.Organization);
    }

    /// <summary>يطبع اختلاف التعريف والتشكيل والتأنيث المنفصل دون إسقاط تخصص أو رتبة أو أرقام.</summary>
    private static string TitleKey(string title) => string.Join(' ', CategorySimilarityRules.Normalize(title)
        .Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(x => x != "ة"));

    /// <summary>يرفض المطابقة البديلة عندما تتكرر هوية الدور في الرد نفسه.</summary>
    private static bool SameSignature(ExtractedOpportunity a, ExtractedOpportunity b) =>
        a.Kind == b.Kind && TitleKey(a.Title) == TitleKey(b.Title)
        && TextRules.OrganizationKey(a.Organization) == TextRules.OrganizationKey(b.Organization)
        && ExplicitWorkLocationRules.Compatible(a.Locations, b.Locations)
        && (a.ReferenceNumber.Length == 0 || b.ReferenceNumber.Length == 0 || DuplicateMatcher.ReferenceKey(a.ReferenceNumber) == DuplicateMatcher.ReferenceKey(b.ReferenceNumber));
}
