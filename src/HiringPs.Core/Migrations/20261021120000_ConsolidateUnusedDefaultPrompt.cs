using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace HiringPs.Core.Migrations;

[DbContext(typeof(HiringDb))]
[Migration("20261021120000_ConsolidateUnusedDefaultPrompt")]
public sealed class ConsolidateUnusedDefaultPrompt : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Retained migration ID for existing databases. Prompt data is initialized
        // only by InitializeCurrentDefaultPrompt after all schema migrations.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // No prompt data is created or removed by this retired initialization step.
    }
}
