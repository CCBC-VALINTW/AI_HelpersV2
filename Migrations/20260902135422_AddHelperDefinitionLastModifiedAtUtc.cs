using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiHelpers.Migrations
{
    /// <inheritdoc />
    public partial class AddHelperDefinitionLastModifiedAtUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfilled to this migration's own apply time, not the year-1 sentinel EF scaffolds
            // by default (see AddFeedbackCreatedAtUtc for that precedent) - V1 never had an
            // equivalent column (see HelperDefinition.LastModifiedAtUtc's own doc comment), so
            // there's no real historical value to recover; "as of when this shipped" is a far more
            // useful placeholder for a column a list page actually displays than 0001-01-01 would
            // be.
            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedAtUtc",
                table: "HelperDefinitions",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(2026, 9, 2, 13, 54, 22, DateTimeKind.Utc));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastModifiedAtUtc",
                table: "HelperDefinitions");
        }
    }
}
