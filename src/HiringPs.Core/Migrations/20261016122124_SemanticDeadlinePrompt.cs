using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HiringPs.Core.Migrations;

public partial class SemanticDeadlinePrompt : Migration
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
