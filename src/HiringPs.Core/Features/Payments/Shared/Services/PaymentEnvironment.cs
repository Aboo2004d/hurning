using Microsoft.Extensions.Hosting;

namespace HiringPs.Core;

// Runtime environment selects the profile; a copied JSON file cannot turn a
// Production host into a sandbox or send development requests to live paths.
/// <summary>يحدد بيئة الدفع من بيئة الاستضافة، ويفصل Sandbox عن Production دون تبديل يدوي لكل طلب.</summary>
public sealed class PaymentEnvironment(IHostEnvironment environment)
{
    public bool IsSandbox => !environment.IsProduction();
    public bool ShowDevelopmentHints => environment.IsDevelopment();
    public string Name => IsSandbox ? "Sandbox" : "Production";
}
