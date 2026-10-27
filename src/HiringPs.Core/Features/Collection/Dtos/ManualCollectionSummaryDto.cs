namespace HiringPs.Core;

/// <summary>حصيلة الجولة اليدوية؛ الإكمال لا يعني أن العناصر المنتظرة لـAI نُشرت.</summary>
public sealed record ManualCollectionSummaryDto(int Sources, int FailedSources, int ProcessedItems, int PendingItems);
