using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiHelpers.Migrations
{
    /// <inheritdoc />
    public partial class MakeHelperLlmOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HelperDefinitions_LlmDefinitions_LlmDefinitionId",
                table: "HelperDefinitions");

            migrationBuilder.AlterColumn<int>(
                name: "LlmDefinitionId",
                table: "HelperDefinitions",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_HelperDefinitions_LlmDefinitions_LlmDefinitionId",
                table: "HelperDefinitions",
                column: "LlmDefinitionId",
                principalTable: "LlmDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HelperDefinitions_LlmDefinitions_LlmDefinitionId",
                table: "HelperDefinitions");

            migrationBuilder.AlterColumn<int>(
                name: "LlmDefinitionId",
                table: "HelperDefinitions",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_HelperDefinitions_LlmDefinitions_LlmDefinitionId",
                table: "HelperDefinitions",
                column: "LlmDefinitionId",
                principalTable: "LlmDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
