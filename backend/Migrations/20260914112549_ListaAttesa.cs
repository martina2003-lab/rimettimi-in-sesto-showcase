using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RimettimiInSesto.Api.Migrations
{
    /// <inheritdoc />
    public partial class ListaAttesa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AvvisiDisponibilita",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PazienteId = table.Column<int>(type: "INTEGER", nullable: false),
                    AppuntamentoId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatoIl = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Stato = table.Column<string>(type: "TEXT", nullable: false),
                    AvvisatoIl = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvvisiDisponibilita", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AvvisiDisponibilita_Appuntamenti_AppuntamentoId",
                        column: x => x.AppuntamentoId,
                        principalTable: "Appuntamenti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AvvisiDisponibilita_Pazienti_PazienteId",
                        column: x => x.PazienteId,
                        principalTable: "Pazienti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AvvisiDisponibilita_AppuntamentoId",
                table: "AvvisiDisponibilita",
                column: "AppuntamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_AvvisiDisponibilita_PazienteId",
                table: "AvvisiDisponibilita",
                column: "PazienteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AvvisiDisponibilita");
        }
    }
}
