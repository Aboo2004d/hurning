using HiringPs.Core;
using HiringPs.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web.Areas.Admin.Controllers;

public sealed class DistributionModel
{
    public bool BotReady { get; init; }
    public List<DistributionTarget> Targets { get; init; } = [];
    public AdminPage<TelegramChannelPostSummaryDto> Posts { get; init; } = new();
    public bool IndexNowEnabled { get; init; }
    public bool IndexNowKeyConfigured { get; init; }
    public string IndexNowKeyUrl { get; init; } = "";
    public int IndexNowPending { get; init; }
    public int IndexNowFailing { get; init; }
    public DateTimeOffset? IndexNowLastSubmission { get; init; }
    public List<IndexNowEntrySummaryDto> IndexNowRecent { get; init; } = [];
}

// Telegram channel auto-publishing and IndexNow status. Sending happens in the Worker,
// except the explicit test post, which is a single admin-requested message.
