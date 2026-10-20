namespace HiringPs.Core;

/// <summary>Result of an administration update; no provider success or payment entitlement is implied.</summary>
public enum RecordUpdateOutcome
{
    Saved = 0,
    NotFound = 1,
    Conflict = 2,
}
