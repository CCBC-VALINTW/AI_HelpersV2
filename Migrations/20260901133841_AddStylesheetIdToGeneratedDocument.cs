using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiHelpers.Migrations
{
    /// <inheritdoc />
    public partial class AddStylesheetIdToGeneratedDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StylesheetId",
                table: "GeneratedDocuments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedDocuments_StylesheetId",
                table: "GeneratedDocuments",
                column: "StylesheetId");

            migrationBuilder.AddForeignKey(
                name: "FK_GeneratedDocuments_Stylesheets_StylesheetId",
                table: "GeneratedDocuments",
                column: "StylesheetId",
                principalTable: "Stylesheets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GeneratedDocuments_Stylesheets_StylesheetId",
                table: "GeneratedDocuments");

            migrationBuilder.DropIndex(
                name: "IX_GeneratedDocuments_StylesheetId",
                table: "GeneratedDocuments");

            migrationBuilder.DropColumn(
                name: "StylesheetId",
                table: "GeneratedDocuments");
        }
    }
}
