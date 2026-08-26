using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiHelpers.Migrations
{
    /// <inheritdoc />
    public partial class AllowDuplicateLlmIdentifier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LlmDefinitions_Identifier",
                table: "LlmDefinitions");

            migrationBuilder.CreateIndex(
                name: "IX_LlmDefinitions_Identifier",
                table: "LlmDefinitions",
                column: "Identifier");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LlmDefinitions_Identifier",
                table: "LlmDefinitions");

            migrationBuilder.CreateIndex(
                name: "IX_LlmDefinitions_Identifier",
                table: "LlmDefinitions",
                column: "Identifier",
                unique: true);
        }
    }
}
