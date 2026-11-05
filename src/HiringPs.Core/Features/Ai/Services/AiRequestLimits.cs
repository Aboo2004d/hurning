using Microsoft.Extensions.Configuration;

namespace HiringPs.Core;

public static class AiRequestLimits
{
    public static int TimeoutSeconds(IConfiguration config) =>
        Math.Clamp(config.GetValue<int?>("Ai:TimeoutSeconds") ?? 180, 30, 300);
}
