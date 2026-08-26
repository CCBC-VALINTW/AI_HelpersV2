using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiHelpers.Migrations
{
    /// <inheritdoc />
    public partial class IntroduceGenericModelDials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReasoningTokens",
                table: "HelperDefinitions");

            migrationBuilder.DropColumn(
                name: "SupportsReasoning",
                table: "HelperDefinitions");

            migrationBuilder.RenameColumn(
                name: "DefaultTemperature",
                table: "LlmDefinitions",
                newName: "DefaultCreativity");

            migrationBuilder.RenameColumn(
                name: "DefaultTopP",
                table: "LlmDefinitions",
                newName: "DefaultAdherence");

            migrationBuilder.RenameColumn(
                name: "TemperatureAdjustmentAllowance",
                table: "HelperDefinitions",
                newName: "CreativityAdjustmentAllowance");

            migrationBuilder.RenameColumn(
                name: "Temperature",
                table: "HelperDefinitions",
                newName: "Creativity");

            migrationBuilder.RenameColumn(
                name: "TopPAdjustmentAllowance",
                table: "HelperDefinitions",
                newName: "AdherenceAdjustmentAllowance");

            migrationBuilder.RenameColumn(
                name: "TopP",
                table: "HelperDefinitions",
                newName: "Adherence");

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "LlmDefinitions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "AwsBedrock");

            migrationBuilder.AddColumn<string>(
                name: "Effort",
                table: "HelperDefinitions",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Provider",
                table: "LlmDefinitions");

            migrationBuilder.DropColumn(
                name: "Effort",
                table: "HelperDefinitions");

            migrationBuilder.RenameColumn(
                name: "DefaultCreativity",
                table: "LlmDefinitions",
                newName: "DefaultTemperature");

            migrationBuilder.RenameColumn(
                name: "DefaultAdherence",
                table: "LlmDefinitions",
                newName: "DefaultTopP");

            migrationBuilder.RenameColumn(
                name: "CreativityAdjustmentAllowance",
                table: "HelperDefinitions",
                newName: "TemperatureAdjustmentAllowance");

            migrationBuilder.RenameColumn(
                name: "Creativity",
                table: "HelperDefinitions",
                newName: "Temperature");

            migrationBuilder.RenameColumn(
                name: "AdherenceAdjustmentAllowance",
                table: "HelperDefinitions",
                newName: "TopPAdjustmentAllowance");

            migrationBuilder.RenameColumn(
                name: "Adherence",
                table: "HelperDefinitions",
                newName: "TopP");

            migrationBuilder.AddColumn<int>(
                name: "ReasoningTokens",
                table: "HelperDefinitions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SupportsReasoning",
                table: "HelperDefinitions",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
