using Microsoft.Extensions.Hosting;

namespace HiringPs.Core;

// Runtime environment selects the profile; a copied JSON file cannot turn a
// Production host into a sandbox or send development requests to live paths.
public sealed class PaymentEnvironment(IHostEnvironment environment)
{
    public bool IsSandbox => !environment.IsProduction();
    public bool ShowDevelopmentHints => environment.IsDevelopment();
    public string Name => IsSandbox ? "Sandbox" : "Production";
}
