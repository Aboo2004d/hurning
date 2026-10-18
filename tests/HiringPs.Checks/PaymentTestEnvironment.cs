using HiringPs.Core;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

public static class PaymentTestEnvironment
{
    public static PaymentEnvironment Development => new(new FixtureHost("Development"));
    public static PaymentEnvironment Staging => new(new FixtureHost("Staging"));
    public static PaymentEnvironment Production => new(new FixtureHost("Production"));

    private sealed class FixtureHost(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "PaymentChecks";
        public string ContentRootPath { get; set; } = "/tmp";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
