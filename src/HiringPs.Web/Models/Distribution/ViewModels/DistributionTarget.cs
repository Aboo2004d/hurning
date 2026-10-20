using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Areas.Admin.Controllers;

public sealed class DistributionTarget
{
    public string Target { get; init; } = "";
    public string Channel { get; init; } = "";
    public TelegramChannelSettingSummaryDto? Setting { get; init; }
}
