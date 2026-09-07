using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiHelpers.Migrations
{
    /// <inheritdoc />
    public partial class AddDataSourceDefinitionOwningHelper : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OwningHelperId",
                table: "DataSourceDefinitions",
                type: "int",
                nullable: true);

            // Hand-added backfill - every DataSourceDefinition that predates this migration was
            // created before the "owning Helper" concept existed at all, so there's no original
            // authorship to recover; the best available substitute is whichever Helper currently
            // attaches it (at this point, always exactly one - reuse across Helpers didn't exist
            // yet either), same reasoning as the AddSharedDataSourceDefinitions migration's own
            // OwnerEmail backfill. A definition with more than one attachment (impossible today,
            // but not asserted against) or none at all is simply left with a null OwningHelperId -
            // safe per that column's own doc comment, not an error.
            migrationBuilder.Sql(@"
                UPDATE d
                SET d.OwningHelperId = q.HelperDefinitionId
                FROM DataSourceDefinitions d
                JOIN HelperDataQueries q ON q.DataSourceDefinitionId = d.Id
                WHERE d.OwningHelperId IS NULL;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_DataSourceDefinitions_OwningHelperId",
                table: "DataSourceDefinitions",
                column: "OwningHelperId");

            migrationBuilder.AddForeignKey(
                name: "FK_DataSourceDefinitions_HelperDefinitions_OwningHelperId",
                table: "DataSourceDefinitions",
                column: "OwningHelperId",
                principalTable: "HelperDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DataSourceDefinitions_HelperDefinitions_OwningHelperId",
                table: "DataSourceDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_DataSourceDefinitions_OwningHelperId",
                table: "DataSourceDefinitions");

            migrationBuilder.DropColumn(
                name: "OwningHelperId",
                table: "DataSourceDefinitions");
        }
    }
}
