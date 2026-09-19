using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RimettimiInSesto.Api.Migrations
{
    /// <inheritdoc />
    public partial class SlotUnicoPerFisioterapista : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appuntamenti_FisioterapistaId",
                table: "Appuntamenti");

            migrationBuilder.CreateIndex(
                name: "IX_Appuntamenti_FisioterapistaId_DataOra",
                table: "Appuntamenti",
                columns: new[] { "FisioterapistaId", "DataOra" },
                unique: true,
                filter: "[Stato] IN (0, 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appuntamenti_FisioterapistaId_DataOra",
                table: "Appuntamenti");

            migrationBuilder.CreateIndex(
                name: "IX_Appuntamenti_FisioterapistaId",
                table: "Appuntamenti",
                column: "FisioterapistaId");
        }
    }
}
