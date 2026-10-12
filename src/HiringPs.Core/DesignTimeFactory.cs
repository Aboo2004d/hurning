using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace HiringPs.Core;

public class DesignTimeFactory : IDesignTimeDbContextFactory<HiringDb>
{
    public HiringDb CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Hiring") ?? "Server=(localdb)\\MSSQLLocalDB;Database=HiringPs_Development;Trusted_Connection=True;TrustServerCertificate=True";
        return new(new DbContextOptionsBuilder<HiringDb>().UseSqlServer(connection).Options);
    }
}
