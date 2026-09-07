using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiHelpers.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedDataSourceDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hand-corrected throughout - the scaffolded version renamed HelperDataQueries.MaxRows
            // straight into the new DataSourceDefinitionId column (both plain ints, so EF's diff
            // treated it as a rename), which would have left every existing row's
            // DataSourceDefinitionId holding its old MaxRows VALUE (e.g. 500) instead of a real
            // DataSourceDefinition id - silent data corruption, not just a missed default like the
            // AddHelperDataQueryCompactionEnabled migration's own defaultValue fix. Rewritten below
            // to actually migrate the data: create DataSourceDefinitions, copy each existing
            // HelperDataQuery's query/connection/format/etc. into it (reusing the same Id via
            // IDENTITY_INSERT, so the two tables correlate 1:1 without a separate mapping step),
            // THEN add DataSourceDefinitionId to HelperDataQueries and backfill it from that same
            // shared Id, and only then drop the old now-redundant columns.
            migrationBuilder.DropForeignKey(
                name: "FK_HelperDataQueries_DataConnections_DataConnectionId",
                table: "HelperDataQueries");

            migrationBuilder.DropIndex(
                name: "IX_HelperDataQueries_DataConnectionId",
                table: "HelperDataQueries");

            migrationBuilder.CreateTable(
                name: "DataSourceDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DataConnectionId = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Query = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OutputFormat = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    MaxRows = table.Column<int>(type: "int", nullable: false),
                    CompactionEnabled = table.Column<bool>(type: "bit", nullable: false),
                    OwnerEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsShared = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataSourceDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataSourceDefinitions_DataConnections_DataConnectionId",
                        column: x => x.DataConnectionId,
                        principalTable: "DataConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // One DataSourceDefinition per existing HelperDataQuery, same Id (via IDENTITY_INSERT)
            // so the backfill below is a trivial self-join on Id. OwnerEmail inherits the owning
            // Helper's OwnerEmail, falling back to the "system" sentinel (never a fabricated email)
            // for a General-scope Helper that itself has none - only an admin can manage those
            // definitions afterwards, same as any other ownerless resource in this app. IsShared
            // starts false for every migrated Data Source - reuse across Helpers is opt-in, not
            // retroactively granted.
            migrationBuilder.Sql(@"
                SET IDENTITY_INSERT DataSourceDefinitions ON;
                INSERT INTO DataSourceDefinitions (Id, DataConnectionId, Label, Query, OutputFormat, MaxRows, CompactionEnabled, OwnerEmail, IsShared, CreatedAtUtc, LastModifiedAtUtc)
                SELECT q.Id, q.DataConnectionId, q.Label, q.Query, q.OutputFormat, q.MaxRows, q.CompactionEnabled,
                       COALESCE(h.OwnerEmail, 'system'), CAST(0 AS bit), SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM HelperDataQueries q
                INNER JOIN HelperDefinitions h ON h.Id = q.HelperDefinitionId;
                SET IDENTITY_INSERT DataSourceDefinitions OFF;
                DBCC CHECKIDENT ('DataSourceDefinitions', RESEED);
            ");

            migrationBuilder.AddColumn<int>(
                name: "DataSourceDefinitionId",
                table: "HelperDataQueries",
                type: "int",
                nullable: true);

            // Valid precisely because the insert above gave each copied DataSourceDefinition the
            // same Id as the HelperDataQuery it came from.
            migrationBuilder.Sql("UPDATE HelperDataQueries SET DataSourceDefinitionId = Id;");

            migrationBuilder.AlterColumn<int>(
                name: "DataSourceDefinitionId",
                table: "HelperDataQueries",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "CompactionEnabled",
                table: "HelperDataQueries");

            migrationBuilder.DropColumn(
                name: "DataConnectionId",
                table: "HelperDataQueries");

            migrationBuilder.DropColumn(
                name: "Label",
                table: "HelperDataQueries");

            migrationBuilder.DropColumn(
                name: "OutputFormat",
                table: "HelperDataQueries");

            migrationBuilder.DropColumn(
                name: "Query",
                table: "HelperDataQueries");

            migrationBuilder.DropColumn(
                name: "MaxRows",
                table: "HelperDataQueries");

            migrationBuilder.CreateIndex(
                name: "IX_HelperDataQueries_DataSourceDefinitionId",
                table: "HelperDataQueries",
                column: "DataSourceDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_DataSourceDefinitions_DataConnectionId",
                table: "DataSourceDefinitions",
                column: "DataConnectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_HelperDataQueries_DataSourceDefinitions_DataSourceDefinitionId",
                table: "HelperDataQueries",
                column: "DataSourceDefinitionId",
                principalTable: "DataSourceDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HelperDataQueries_DataSourceDefinitions_DataSourceDefinitionId",
                table: "HelperDataQueries");

            migrationBuilder.DropIndex(
                name: "IX_HelperDataQueries_DataSourceDefinitionId",
                table: "HelperDataQueries");

            migrationBuilder.AddColumn<int>(
                name: "DataConnectionId",
                table: "HelperDataQueries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Label",
                table: "HelperDataQueries",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Query",
                table: "HelperDataQueries",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutputFormat",
                table: "HelperDataQueries",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxRows",
                table: "HelperDataQueries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CompactionEnabled",
                table: "HelperDataQueries",
                type: "bit",
                nullable: true);

            // Reverses the Up() split - every HelperDataQuery's DataSourceDefinitionId still equals
            // the Id of the DataSourceDefinition it was originally split from (Up() guaranteed
            // that), so this join recovers the original values exactly, including for any Data
            // Source since edited or reused by another Helper (the values just reflect its current
            // state, same as the split's own live-reference design intends).
            migrationBuilder.Sql(@"
                UPDATE q
                SET q.DataConnectionId = d.DataConnectionId,
                    q.Label = d.Label,
                    q.Query = d.Query,
                    q.OutputFormat = d.OutputFormat,
                    q.MaxRows = d.MaxRows,
                    q.CompactionEnabled = d.CompactionEnabled
                FROM HelperDataQueries q
                INNER JOIN DataSourceDefinitions d ON d.Id = q.DataSourceDefinitionId;
            ");

            migrationBuilder.DropColumn(
                name: "DataSourceDefinitionId",
                table: "HelperDataQueries");

            migrationBuilder.DropTable(
                name: "DataSourceDefinitions");

            migrationBuilder.AlterColumn<int>(
                name: "DataConnectionId",
                table: "HelperDataQueries",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Label",
                table: "HelperDataQueries",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Query",
                table: "HelperDataQueries",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OutputFormat",
                table: "HelperDataQueries",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "MaxRows",
                table: "HelperDataQueries",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "CompactionEnabled",
                table: "HelperDataQueries",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HelperDataQueries_DataConnectionId",
                table: "HelperDataQueries",
                column: "DataConnectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_HelperDataQueries_DataConnections_DataConnectionId",
                table: "HelperDataQueries",
                column: "DataConnectionId",
                principalTable: "DataConnections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
