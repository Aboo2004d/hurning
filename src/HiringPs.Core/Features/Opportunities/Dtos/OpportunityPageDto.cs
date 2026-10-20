namespace HiringPs.Core;

/// <summary>نتائج صفحة بطاقات وعددها الكلي؛ لا تعتمد على MVC أو كيان EF.</summary>
public sealed record OpportunityPageDto(List<OpportunityCardDto> Items, int Count);
