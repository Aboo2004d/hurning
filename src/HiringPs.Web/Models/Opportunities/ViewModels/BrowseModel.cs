using System.Globalization;
using HiringPs.Core;

namespace HiringPs.Web.Models;

public sealed class BrowseModel : ListingModel
{
    public string Path { get; set; } = "/";
    public string Heading { get; set; } = "";
    public string Intro { get; set; } = "";
    public City? BrowseCity { get; set; }
    public OrganizationFacet? Organization { get; set; }
}
