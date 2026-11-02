namespace HiringPs.Core;

/// <summary>Independent optional filters; null means all values of that criterion.</summary>
public sealed record TelegramJobFilters(string? City = null, string? Organization = null, string? Category = null)
{
    public static TelegramJobFilters Empty { get; } = new();
    public string? Value(string field) => field switch
    {
        "city" => City, "organization" => Organization, "category" => Category, _ => null,
    };
    public TelegramJobFilters With(string field, string? value) => field switch
    {
        "city" => this with { City = value },
        "organization" => this with { Organization = value },
        "category" => this with { Category = value },
        _ => this,
    };
}
