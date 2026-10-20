namespace HiringPs.Core;

// All probes are read-only. They never initialize a payment or send an OTP.
/// <summary>فحوص بوابات الدفع للقراءة فقط؛ لا تنشئ دفعة أو ترسل OTP.</summary>
public sealed class PaymentToolProbes(LahzaGateway lahza, JawwalPayGateway jawwal)
{
    /// <summary>يفحص بوابة الدفع المطلوبة باتصال للقراءة دون خصم أو إرسال OTP.</summary>
    public async Task<ToolProbe> CheckAsync(string tool, CancellationToken ct)
    {
        IPaymentGateway gateway = tool == "LahzaPayment" ? lahza : jawwal;
        if (!gateway.Ready)
            return new(
                tool,
                ToolState.NotConfigured,
                "PaymentCredentialsMissing",
                gateway.Environment
            );
        try
        {
            if (tool == "LahzaPayment")
                await lahza.ProbeAsync(ct);
            else
                await jawwal.ProbeAsync(ct);
            return new(tool, ToolState.Ready, "PaymentProviderResponded", gateway.Environment);
        }
        catch (PaymentProviderException)
        {
            return new(tool, ToolState.Problem, "PaymentProviderUnavailable", gateway.Environment);
        }
    }
}
