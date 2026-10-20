using Microsoft.EntityFrameworkCore;

namespace HiringPs.Core;

/// <summary>يفصل البوابات المتاحة للبيع عن المحولات التي يمكنها التحقق من دفعات سابقة.</summary>
public sealed class PaymentGatewayRegistry(
    HiringDb db,
    Microsoft.Extensions.Configuration.IConfiguration configuration,
    IEnumerable<IPaymentGateway> gateways
)
{
    // Closing new sales must not prevent confirming an existing charge.
    /// <summary>يجد محول البوابة حتى عندما تكون المبيعات الجديدة عبرها متوقفة.</summary>
    public IPaymentGateway? FindAdapter(string key) => gateways.SingleOrDefault(x => x.Key == key);

    /// <summary>يختار محولًا مهيأ للتحقق من دفعات قائمة دون اشتراط فتح مبيعات جديدة.</summary>
    public IPaymentGateway? ForVerification(string key) =>
        gateways.SingleOrDefault(x => x.Key == key && x.Ready);

    /// <summary>يقرأ البوابات الجاهزة والمفعلة في الإعدادات وقاعدة البيانات للمبيعات الجديدة.</summary>
    public async Task<IPaymentGateway[]> AvailableAsync(CancellationToken ct)
    {
        var enabled = await db
            .PaymentGatewaySettings.AsNoTracking()
            .Where(x => x.Enabled)
            .Select(x => x.Key)
            .ToListAsync(ct);
        return gateways
            .Where(x =>
                x.Ready
                && enabled.Contains(x.Key)
                && bool.TryParse(
                    configuration["Payments:" + x.Key + ":Enabled"],
                    out var configured
                )
                && configured
            )
            .ToArray();
    }
}
