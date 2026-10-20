using HiringPs.Core;

namespace HiringPs.Web.Models;

public sealed record PaymentEndpoint(string Method, string Address, string Label);
