using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiHelpers.Migrations
{
    /// <inheritdoc />
    // Hand-edited after scaffolding: the default scaffold was a Drop+Add pair (real data loss -
    // every existing InputTokenCost/OutputTokenCost value would vanish). Rewritten as a rename +
    // widen + convert instead, so existing per-1,000 rates survive as their per-million equivalent.
    public partial class RenameTokenCostsToPerMillion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "InputTokenCost",
                table: "LlmDefinitions",
                newName: "InputCostPerMillionTokens");

            migrationBuilder.RenameColumn(
                name: "OutputTokenCost",
                table: "LlmDefinitions",
                newName: "OutputCostPerMillionTokens");

            // The rename alone leaves every existing rate meaning "per 1,000 tokens" in a column
            // now named "per million" - this is the actual re-basing, not just a schema change.
            // Deliberately done BEFORE the AlterColumn below narrows scale 5 -> 4: multiplying
            // first, while the column still has its original decimal(8,5) precision, keeps every
            // value exact (e.g. .00039 -> .39000, still fits scale 5) - multiplying after the
            // narrowing would round the ORIGINAL .00039 to decimal(_,4) first (-> .0004) and only
            // then multiply, silently corrupting any rate whose 5th decimal digit wasn't zero.
            migrationBuilder.Sql(
                "UPDATE LlmDefinitions SET InputCostPerMillionTokens = InputCostPerMillionTokens * 1000 " +
                "WHERE InputCostPerMillionTokens IS NOT NULL;");
            migrationBuilder.Sql(
                "UPDATE LlmDefinitions SET OutputCostPerMillionTokens = OutputCostPerMillionTokens * 1000 " +
                "WHERE OutputCostPerMillionTokens IS NOT NULL;");

            migrationBuilder.AlterColumn<decimal>(
                name: "InputCostPerMillionTokens",
                table: "LlmDefinitions",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(8,5)",
                oldPrecision: 8,
                oldScale: 5,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "OutputCostPerMillionTokens",
                table: "LlmDefinitions",
                type: "decimal(10,4)",
                precision: 10,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(8,5)",
                oldPrecision: 8,
                oldScale: 5,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Mirrors Up's ordering: widen back to scale 5 BEFORE dividing, so the division itself
            // never loses precision either.
            migrationBuilder.AlterColumn<decimal>(
                name: "InputCostPerMillionTokens",
                table: "LlmDefinitions",
                type: "decimal(8,5)",
                precision: 8,
                scale: 5,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,4)",
                oldPrecision: 10,
                oldScale: 4,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "OutputCostPerMillionTokens",
                table: "LlmDefinitions",
                type: "decimal(8,5)",
                precision: 8,
                scale: 5,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,4)",
                oldPrecision: 10,
                oldScale: 4,
                oldNullable: true);

            migrationBuilder.Sql(
                "UPDATE LlmDefinitions SET InputCostPerMillionTokens = InputCostPerMillionTokens / 1000 " +
                "WHERE InputCostPerMillionTokens IS NOT NULL;");
            migrationBuilder.Sql(
                "UPDATE LlmDefinitions SET OutputCostPerMillionTokens = OutputCostPerMillionTokens / 1000 " +
                "WHERE OutputCostPerMillionTokens IS NOT NULL;");

            migrationBuilder.RenameColumn(
                name: "InputCostPerMillionTokens",
                table: "LlmDefinitions",
                newName: "InputTokenCost");

            migrationBuilder.RenameColumn(
                name: "OutputCostPerMillionTokens",
                table: "LlmDefinitions",
                newName: "OutputTokenCost");
        }
    }
}
