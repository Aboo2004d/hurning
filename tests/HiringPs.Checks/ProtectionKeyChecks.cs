using System.Xml;
using System.Xml.Linq;
using HiringPs.Core;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public static class ProtectionKeyChecks
{
    public static void Run()
    {
        var count = 0;
        void Check(bool value, string description)
        {
            if (!value) throw new Exception(description);
            count++;
        }
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Hiring"] = "Server=.;Database=metadata",
            ["DataProtection:KeyPath"] = "/this-legacy-path-must-not-be-used"
        }).Build();
        ServiceProvider Provider(IXmlRepository? repository = null)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddHiringDataProtection();
            if (repository is not null) services.AddSingleton(repository);
            return services.BuildServiceProvider();
        }
        using (var configured = Provider())
        {
            var options = configured.GetRequiredService<IOptions<KeyManagementOptions>>().Value;
            Check(options.XmlRepository is SqlProtectionKeyRepository, "SQL is the only configured key repository");
            Check(options.XmlEncryptor is null, "No host-specific DPAPI dependency");
            Check(configured.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator == "HiringPs", "Application discriminator preserved");
        }
        var shared = new MemoryKeys();
        string token;
        using (var first = Provider(shared))
        {
            token = first.GetRequiredService<IDataProtectionProvider>().CreateProtector("HiringPs.SocialToken.v1").Protect("fixture-token");
            Check(shared.GetAllElements().Count > 0, "Framework persists generated key in configured repository");
        }
        using (var restarted = Provider(shared))
        {
            var provider = restarted.GetRequiredService<IDataProtectionProvider>();
            Check(provider.CreateProtector("HiringPs.SocialToken.v1").Unprotect(token) == "fixture-token", "Second process can decrypt persisted data");
            var session = provider.CreateProtector("test-cookie").Protect("fixture-session");
            Check(provider.CreateProtector("test-cookie").Unprotect(session) == "fixture-session", "Session protection remains enabled");
            var keyManager = restarted.GetRequiredService<IKeyManager>();
            keyManager.CreateNewKey(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(90));
            Check(provider.CreateProtector("HiringPs.SocialToken.v1").Unprotect(token) == "fixture-token", "Rotation preserves older ciphertext");
        }
        Check(SqlProtectionKeyRepository.Parse("<key id='fixture' />").Name.LocalName == "key", "XML key parsing supported");
        var rejected = false;
        try { SqlProtectionKeyRepository.Parse("<!DOCTYPE key [<!ENTITY x SYSTEM 'file:///etc/passwd'>]><key>&x;</key>"); }
        catch (XmlException) { rejected = true; }
        Check(rejected, "External XML entities rejected");
        using var db = new HiringDb(new DbContextOptionsBuilder<HiringDb>().UseSqlServer("Server=.;Database=metadata").Options);
        Check(db.Model.FindEntityType(typeof(ProtectionKeyRecord))!.GetTableName() == "DataProtectionKeys", "Key table mapped independently");
        Console.WriteLine($"Protection keys: {count} checks passed; in-memory repository simulates shared database, no SQL connection made.");
    }

    private sealed class MemoryKeys : IXmlRepository
    {
        private readonly List<XElement> values = [];
        public IReadOnlyCollection<XElement> GetAllElements() => values.Select(x => new XElement(x)).ToArray();
        public void StoreElement(XElement element, string friendlyName) => values.Add(new XElement(element));
    }
}
