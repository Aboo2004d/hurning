using System.Globalization;
using HiringPs.Core;

namespace HiringPs.Web.Models;

public sealed record CardListModel(ListingModel Listing, IEnumerable<Opportunity> Items, bool Compact = false);
