using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RimettimiInSesto.Api.Migrations
{
    /// <inheritdoc />
    public partial class AggiungeDocumentoCaricatoRicetta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DocumentoCaricato",
                table: "Ricette",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentoCaricato",
                table: "Ricette");
        }
    }
}
