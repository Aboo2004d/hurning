using HiringPs.Core;
namespace HiringPs.Web.Models;

public class ListingModel
{
    public List<Opportunity> Items { get; set; } = [];
    public int Count { get; set; }
    public int Page { get; set; }
    public List<string> Cities { get; set; } = [];
    public string Query { get; set; } = ""; public OpportunityKind? Kind { get; set; }
    public string City { get; set; } = ""; public string Category { get; set; } = "";
    public string Employment { get; set; } = ""; public string Experience { get; set; } = ""; public bool Archive { get; set; }
    public List<string> Categories { get; set; } = []; public List<string> EmploymentTypes { get; set; } = []; public List<string> ExperienceLevels { get; set; } = [];
}
