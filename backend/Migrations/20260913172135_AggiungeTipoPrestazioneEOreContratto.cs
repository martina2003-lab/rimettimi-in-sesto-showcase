using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RimettimiInSesto.Api.Migrations
{
    /// <inheritdoc />
    public partial class AggiungeTipoPrestazioneEOreContratto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TipoPrestazione",
                table: "Pagamenti",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "OreSettimanaliContratto",
                table: "Fisioterapisti",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TipoPrestazione",
                table: "Pagamenti");

            migrationBuilder.DropColumn(
                name: "OreSettimanaliContratto",
                table: "Fisioterapisti");
        }
    }
}
