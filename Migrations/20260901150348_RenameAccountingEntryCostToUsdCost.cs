using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiHelpers.Migrations
{
    /// <inheritdoc />
    public partial class RenameAccountingEntryCostToUsdCost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Cost",
                table: "AccountingEntries",
                newName: "UsdCost");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UsdCost",
                table: "AccountingEntries",
                newName: "Cost");
        }
    }
}
