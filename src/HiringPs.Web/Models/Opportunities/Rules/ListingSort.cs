using System.Globalization;
using HiringPs.Core;

namespace HiringPs.Web.Models;

public static class ListingSort
{
    public const string Newest = "newest";
    public const string Closing = "closing";

    public static string Parse(string? value) => value == Closing ? Closing : Newest;
}

/// <summary>Indexable landing page for one kind in one city, or for one organization.</summary>
