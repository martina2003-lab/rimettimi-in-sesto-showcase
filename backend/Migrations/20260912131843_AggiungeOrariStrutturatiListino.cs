using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RimettimiInSesto.Api.Migrations
{
    /// <inheritdoc />
    public partial class AggiungeOrariStrutturatiListino : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "MattinaFine",
                table: "ImpostazioniListino",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "MattinaInizio",
                table: "ImpostazioniListino",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "PomeriggioFine",
                table: "ImpostazioniListino",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "PomeriggioInizio",
                table: "ImpostazioniListino",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MattinaFine",
                table: "ImpostazioniListino");

            migrationBuilder.DropColumn(
                name: "MattinaInizio",
                table: "ImpostazioniListino");

            migrationBuilder.DropColumn(
                name: "PomeriggioFine",
                table: "ImpostazioniListino");

            migrationBuilder.DropColumn(
                name: "PomeriggioInizio",
                table: "ImpostazioniListino");
        }
    }
}
