using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RimettimiInSesto.Api.Migrations
{
    /// <inheritdoc />
    public partial class AggiungeRichiestaModificaAppuntamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ModificaRichiestaDataOra",
                table: "Appuntamenti",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModificaRichiestaIl",
                table: "Appuntamenti",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModificaRichiestaDataOra",
                table: "Appuntamenti");

            migrationBuilder.DropColumn(
                name: "ModificaRichiestaIl",
                table: "Appuntamenti");
        }
    }
}
