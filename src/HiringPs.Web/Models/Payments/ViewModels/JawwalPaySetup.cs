using HiringPs.Core;

namespace HiringPs.Web.Models;

// Uses the documented partner API contract; never exposes configured credentials.
public sealed class JawwalPaySetup
{
    public PaymentEndpoint[] Endpoints { get; private init; } = [];
    public bool EndpointsValid { get; private init; }
    public bool Ready { get; set; }
    public bool IsSandbox { get; set; }
    public WalletBalance? Balance { get; set; }
    public DateTimeOffset? BalanceFetchedAt { get; set; }

    public static JawwalPaySetup FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("Payments:JawwalPay");
        var origin = section["BaseUrl"]?.TrimEnd('/');
        var paths = new[]
        {
            (
                "POST",
                section["InitiatePath"],
                "/api/partner/pay/initiate",
                "WalletInitiateEndpoint"
            ),
            ("POST", section["ConfirmPath"], "/api/partner/pay/confirm", "WalletConfirmEndpoint"),
            ("GET", section["BalancePath"], "/api/partner/balance", "WalletBalanceEndpoint"),
            (
                "GET",
                section["TransactionsPath"],
                "/api/partner/transactions",
                "WalletTransactionsEndpoint"
            ),
        };
        var valid = origin == "https://store.portal.ps";
        var endpoints = new List<PaymentEndpoint>();
        foreach (var (method, configured, expected, label) in paths)
        {
            valid &= configured == expected;
            endpoints.Add(new PaymentEndpoint(method, (origin ?? "") + configured, label));
        }
        return new JawwalPaySetup { Endpoints = endpoints.ToArray(), EndpointsValid = valid };
    }
}
