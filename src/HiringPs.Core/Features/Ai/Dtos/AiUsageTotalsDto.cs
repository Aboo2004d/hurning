namespace HiringPs.Core;

/// <summary>Known-token totals grouped by prompt version and model; null totals mean no reported count.</summary>
public sealed class AiUsageTotalsDto
{
    public int PromptVersionId
    {
        get; init;
    }
    public string Model { get; init; } = "";
    public int Requests
    {
        get; init;
    }
    public int Succeeded
    {
        get; init;
    }
    public int Failed
    {
        get; init;
    }
    public int Unfinished
    {
        get; init;
    }
    public int UnknownTokenRequests
    {
        get; init;
    }
    public long? InputTokens
    {
        get; init;
    }
    public long? OutputTokens
    {
        get; init;
    }
    public long? TotalTokens
    {
        get; init;
    }
}
