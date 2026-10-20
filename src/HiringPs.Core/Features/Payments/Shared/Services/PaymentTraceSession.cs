using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HiringPs.Core;

/// <summary>يجمع مراحل محاولة دفع واحدة ويطلب حفظها عند انتهاء النطاق.</summary>
public sealed class PaymentTraceSession(PaymentTraceWriter writer, Guid orderId) : IAsyncDisposable
{
    public Guid Id { get; } = Guid.NewGuid();
    public Guid OrderId { get; } = orderId;
    public string? OrderEnvironment { get; set; }
    public string? RuntimeEnvironment { get; set; }
    public string? Gateway { get; set; }
    public string? AdapterEnvironment { get; set; }
    public WalletReceiptValidationFacts? ReceiptFacts { get; set; }
    public bool Persist { get; set; } = true;
    public List<PaymentTraceStep> Steps { get; } = [];

    /// <summary>يسجل مرحلة تشخيصية محددة دون حمولة مزود أو مفاتيح سرية.</summary>
    public void Step(string stage, string code, int? httpStatus = null)
    {
        if (Steps.Count < 12)
            Steps.Add(new(stage, code, httpStatus));
        writer.Log(this, stage, code, httpStatus);
    }

    /// <summary>يحرر موارد الخدمة أو قفلها أو يحفظ جلسة التشخيص عند انتهاء استخدامها.</summary>
    public ValueTask DisposeAsync() => new(writer.SaveAsync(this));
}
