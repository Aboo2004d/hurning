namespace HiringPs.Core;

/// <summary>Declarative public JSON API mapping; paths are object property names, never executable expressions.</summary>
public sealed class JsonApiConfiguration
{
    public string ListingUrlTemplate { get; set; } = "";
    public string ItemUrlTemplate { get; set; } = "";
    public string DetailUrlTemplate { get; set; } = "";
    public string ItemsPath { get; set; } = "data.items";
    public string DetailPath { get; set; } = "data.job";
    public string IdPath { get; set; } = "id";
    public string SlugPath { get; set; } = "slug";
    public string SuccessPath { get; set; } = "success";
    public string CurrentPagePath { get; set; } = "data.meta.current_page";
    public string LastPagePath { get; set; } = "data.meta.last_page";
    public JsonApiField[] Fields { get; set; } = [];
    public string ContentPath { get; set; } = "description";
}

public sealed record JsonApiField(string Label, string Path, bool Html = false);
