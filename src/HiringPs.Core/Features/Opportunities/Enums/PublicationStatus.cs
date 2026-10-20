namespace HiringPs.Core;

/// <summary>القيم المعتمدة لـPublicationStatus؛ أرقامها ثابتة للمحافظة على البيانات المحفوظة.</summary>
public enum PublicationStatus
{
    Draft = 0,
    NeedsReview = 1,
    Published = 2,
    Archived = 3,
    Hidden = 4,
}
