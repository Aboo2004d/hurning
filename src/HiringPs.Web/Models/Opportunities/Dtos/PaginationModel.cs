using System.Globalization;
using HiringPs.Core;

namespace HiringPs.Web.Models;

public sealed record PaginationModel(int Page, int PageCount, Func<int, string> Url);
