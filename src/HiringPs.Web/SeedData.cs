using HiringPs.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HiringPs.Web;

public static class SeedData
{

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<HiringDb>();

        var roleManager =
            services.GetRequiredService<RoleManager<IdentityRole>>();

        var userManager =
            services.GetRequiredService<UserManager<IdentityUser>>();

        // 1. Seed Roles
        string[] roles = { "Admin" };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(
                    new IdentityRole(role));

                EnsureSuccess(result);
            }
        }
         string AdminEmail = "admin@hiring.ps";

        // 2. Seed Main Admin
        var admin = await userManager.FindByEmailAsync(AdminEmail);

        if (admin == null)
        {
            // Initial password is supplied only during first setup.
            var password = "Admin2026@123";
            
            admin = new IdentityUser
            {
                UserName = AdminEmail,
                Email = AdminEmail,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(
                admin, password);

            EnsureSuccess(result);
        }

        // 3. Assign Admin Role
        if (!await userManager.IsInRoleAsync(admin, "Admin"))
        {
            var result = await userManager.AddToRoleAsync(
                admin, "Admin");

            EnsureSuccess(result);
        }

        // 4. Seed application data
        await SeedSourcesAsync(db);
    }

    private static void EnsureSuccess(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join("; ",
                    result.Errors.Select(e => e.Description)));
        }
    }

    private static async Task SeedSourcesAsync(HiringDb db)
    {
        var oldRecords = await db.Opportunities
            .Where(x => x.Organization != "" &&
                        x.NormalizedOrganization == "")
            .ToListAsync();

        foreach (var record in oldRecords)
        {
            record.NormalizedOrganization =
                TextRules.Normalize(record.Organization);
        }

        if (oldRecords.Count > 0)
            await db.SaveChangesAsync();

        // Seed default sources
        if (!await db.Sources.AnyAsync())
        {
            db.Sources.AddRange(
                new Source
                {
                    Name = "ديوان الموظفين العام",
                    Url = "https://www.gpc.pna.ps/diwan/viewPublicVacancyList.gpc",
                    Connector = ConnectorKind.Gpc
                },
                new Source
                {
                    Name = "PalCareer",
                    Url = "https://palcareer.com/",
                    Connector = ConnectorKind.PalCareer
                },
                new Source
                {
                    Name = "ReliefWeb",
                    Url = OpportunityRss.ReliefUrl,
                    Connector = ConnectorKind.ReliefWebRss
                },
                new Source
                {
                    Name = "Jobs.ps — غزة (RSS)",
                    Url = OpportunityRss.JobsUrl,
                    Connector = ConnectorKind.JobsRss
                },
                new Source
                {
                    Name = "UNRWA",
                    Url = UnCareersFeed.Url,
                    Connector = ConnectorKind.UnrwaInspira
                }
            );

            await db.SaveChangesAsync();
        }

        // Upgrade only the former default UNRWA endpoint; preserve approval and scheduling.
        var legacyUnrwa = await db.Sources.Where(x => x.Url == "https://www.unrwa.org/careers/vacancies").ToListAsync();
        foreach (var source in legacyUnrwa)
        {
            source.Url = UnCareersFeed.Url;
            source.Connector = ConnectorKind.UnrwaInspira;
            source.ConfigurationJson = "{}";
            source.CollectionRetryAt = null;
            source.LastError = "";
        }
        if (legacyUnrwa.Count > 0) await db.SaveChangesAsync();

        // Idempotent source-only upgrades; no recreated database or invented approvals.
        var rssSources = await db.Sources.Where(x => x.Name != "Manual entries" &&
            (x.Url == "https://reliefweb.int/jobs" || x.Url == "https://www.jobs.ps/" || x.Url == "https://www.jobs.ps/jobs" || x.Url == "https://www.jobs.ps/jobs/latest")).ToListAsync();
        foreach (var source in rssSources)
        {
            var relief = source.Url == "https://reliefweb.int/jobs";
            source.Url = relief ? OpportunityRss.ReliefUrl : OpportunityRss.JobsUrl;
            source.Connector = relief ? ConnectorKind.ReliefWebRss : ConnectorKind.JobsRss;
            if (!relief) source.Name = "Jobs.ps — غزة (RSS)";
            source.ConfigurationJson = "{}";
            source.CollectionRetryAt = null;
            source.LastError = "";
        }
        var manualSources = await db.Sources.Where(x => x.Name == "Manual entries").ToListAsync();
        foreach (var source in manualSources) { source.Enabled = false; source.Url = "https://hiring.ps"; }
        if (rssSources.Count > 0 || manualSources.Count > 0) await db.SaveChangesAsync();

        // Seed PRCS Tenders
        var prcsUrl =
            "https://www.palestinercs.org/en/Category/50/tenders";

        if (!await db.Sources.AnyAsync(x => x.Url == prcsUrl))
        {
            db.Sources.Add(new Source
            {
                Name = "PRCS Tenders",
                Url = prcsUrl,
                Connector = ConnectorKind.BrowserSelectors,
                Enabled = false,
                ConfigurationJson =
                    "{\"ListingLinkSelector\":\"a[href*='/tenders/'][href$='.pdf']\",\"ContentSelector\":\"body\",\"MaxPages\":5}"
            });

            await db.SaveChangesAsync();
        }
    }

}
