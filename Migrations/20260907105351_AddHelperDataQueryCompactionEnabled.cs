using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiHelpers.Migrations
{
    /// <inheritdoc />
    public partial class AddHelperDataQueryCompactionEnabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // defaultValue: true, not EF's scaffolded false - this backfills every already-
            // configured Data Source to "on" (matching the entity's own CompactionEnabled = true
            // default), not silently off. EF's migration scaffolding uses the CLR type's bare
            // default here regardless of the property's own C# initializer - it has to be
            // corrected by hand every time, not just this once.
            migrationBuilder.AddColumn<bool>(
                name: "CompactionEnabled",
                table: "HelperDataQueries",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompactionEnabled",
                table: "HelperDataQueries");
        }
    }
}
