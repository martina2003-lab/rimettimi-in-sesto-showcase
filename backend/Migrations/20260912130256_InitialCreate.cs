using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RimettimiInSesto.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Nome = table.Column<string>(type: "TEXT", nullable: false),
                    Cognome = table.Column<string>(type: "TEXT", nullable: false),
                    Ruolo = table.Column<string>(type: "TEXT", nullable: false),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: true),
                    SecurityStamp = table.Column<string>(type: "TEXT", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumber = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChiusureStudio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DataInizio = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DataFine = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Motivo = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChiusureStudio", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImpostazioniAgenda",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FinestraPrenotazioneMassimaGiorni = table.Column<int>(type: "INTEGER", nullable: false),
                    PreavvisoMinimoCancellazioneOre = table.Column<int>(type: "INTEGER", nullable: false),
                    SogliaAvvisoDisponibilitaAnticipataGiorni = table.Column<int>(type: "INTEGER", nullable: false),
                    ValiditaPropostaSlotOre = table.Column<int>(type: "INTEGER", nullable: false),
                    SogliaPriorizzazioneRicettaInIntegrazioneGiorni = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImpostazioniAgenda", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImpostazioniListino",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PrezzoPacchettoPrivato = table.Column<decimal>(type: "TEXT", nullable: false),
                    SedutePacchettoPrivato = table.Column<int>(type: "INTEGER", nullable: false),
                    DurataValiditaPacchettoMesi = table.Column<int>(type: "INTEGER", nullable: false),
                    PrezzoSedutaSingolaManuale = table.Column<decimal>(type: "TEXT", nullable: false),
                    QuotaTicketRegionale = table.Column<decimal>(type: "TEXT", nullable: false),
                    OrariApertura = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImpostazioniListino", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pazienti",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", nullable: false),
                    Cognome = table.Column<string>(type: "TEXT", nullable: false),
                    CodiceFiscale = table.Column<string>(type: "TEXT", nullable: true),
                    Telefono = table.Column<string>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    DataNascita = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Stato = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pazienti", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RoleId = table.Column<string>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderKey = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    RoleId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Fisioterapisti",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UtenteId = table.Column<string>(type: "TEXT", nullable: false),
                    Bio = table.Column<string>(type: "TEXT", nullable: true),
                    FotoProfiloUrl = table.Column<string>(type: "TEXT", nullable: true),
                    TipoContratto = table.Column<string>(type: "TEXT", nullable: false),
                    PatternDisponibilita = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fisioterapisti", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Fisioterapisti_AspNetUsers_UtenteId",
                        column: x => x.UtenteId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogAccessiClinici",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UtenteId = table.Column<string>(type: "TEXT", nullable: false),
                    PazienteId = table.Column<int>(type: "INTEGER", nullable: false),
                    TipoAccesso = table.Column<string>(type: "TEXT", nullable: false),
                    DataOra = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AppuntamentoGiustificativoId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogAccessiClinici", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogAccessiClinici_AspNetUsers_UtenteId",
                        column: x => x.UtenteId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuditLogAccessiClinici_Pazienti_PazienteId",
                        column: x => x.PazienteId,
                        principalTable: "Pazienti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CartelleCliniche",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PazienteId = table.Column<int>(type: "INTEGER", nullable: false),
                    Provenienza = table.Column<string>(type: "TEXT", nullable: true),
                    AnamnesiPatologicaRemota = table.Column<string>(type: "TEXT", nullable: true),
                    EsameObiettivo = table.Column<string>(type: "TEXT", nullable: true),
                    EsamiSpecialistici = table.Column<string>(type: "TEXT", nullable: true),
                    Diagnosi = table.Column<string>(type: "TEXT", nullable: true),
                    ProgrammaRiabilitativo = table.Column<string>(type: "TEXT", nullable: true),
                    IndicazioniPaziente = table.Column<string>(type: "TEXT", nullable: true),
                    NoteSostituzione = table.Column<string>(type: "TEXT", nullable: true),
                    VasIniziale = table.Column<int>(type: "INTEGER", nullable: true),
                    VasFinale = table.Column<int>(type: "INTEGER", nullable: true),
                    DataInizioTerapia = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    DataFineTerapia = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    FirmaFisioterapista = table.Column<string>(type: "TEXT", nullable: true),
                    FirmaMedicoResponsabile = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartelleCliniche", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CartelleCliniche_Pazienti_PazienteId",
                        column: x => x.PazienteId,
                        principalTable: "Pazienti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Consensi",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PazienteId = table.Column<int>(type: "INTEGER", nullable: false),
                    Tipo = table.Column<string>(type: "TEXT", nullable: false),
                    Data = table.Column<DateTime>(type: "TEXT", nullable: false),
                    VersioneInformativa = table.Column<string>(type: "TEXT", nullable: false),
                    Stato = table.Column<string>(type: "TEXT", nullable: false),
                    Firmatario = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Consensi", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Consensi_Pazienti_PazienteId",
                        column: x => x.PazienteId,
                        principalTable: "Pazienti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Controindicazioni",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PazienteId = table.Column<int>(type: "INTEGER", nullable: false),
                    Pacemaker = table.Column<bool>(type: "INTEGER", nullable: false),
                    Gravidanza = table.Column<bool>(type: "INTEGER", nullable: false),
                    NeoplasiaInAttoOPregressa = table.Column<bool>(type: "INTEGER", nullable: false),
                    Epilessia = table.Column<bool>(type: "INTEGER", nullable: false),
                    LesioniCutaneeOFratture = table.Column<bool>(type: "INTEGER", nullable: false),
                    StatoInfiammatorioAcuto = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisturbiCardiocircolatori = table.Column<bool>(type: "INTEGER", nullable: false),
                    MezziDiSintesiOProtesi = table.Column<bool>(type: "INTEGER", nullable: false),
                    GraveOsteoporosi = table.Column<bool>(type: "INTEGER", nullable: false),
                    TendenzaEmorragie = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProtesiAcustiche = table.Column<bool>(type: "INTEGER", nullable: false),
                    AllergiaFans = table.Column<bool>(type: "INTEGER", nullable: false),
                    InterventiChirurgici = table.Column<string>(type: "TEXT", nullable: true),
                    TerapieFarmacologicheInAtto = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Controindicazioni", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Controindicazioni_Pazienti_PazienteId",
                        column: x => x.PazienteId,
                        principalTable: "Pazienti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NoteOperative",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PazienteId = table.Column<int>(type: "INTEGER", nullable: false),
                    Testo = table.Column<string>(type: "TEXT", nullable: false),
                    Autore = table.Column<string>(type: "TEXT", nullable: false),
                    Data = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteOperative", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoteOperative_Pazienti_PazienteId",
                        column: x => x.PazienteId,
                        principalTable: "Pazienti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ricette",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PazienteId = table.Column<int>(type: "INTEGER", nullable: false),
                    NumeroONre = table.Column<string>(type: "TEXT", nullable: true),
                    MedicoPrescrittore = table.Column<string>(type: "TEXT", nullable: true),
                    DataEmissione = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Scadenza = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    FinestraCompletamento = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    CodicePrestazioneBranca = table.Column<string>(type: "TEXT", nullable: true),
                    DistrettiCorporei = table.Column<string>(type: "TEXT", nullable: true),
                    QuesitoDiagnostico = table.Column<string>(type: "TEXT", nullable: true),
                    Esenzione = table.Column<bool>(type: "INTEGER", nullable: false),
                    CodiceEsenzione = table.Column<string>(type: "TEXT", nullable: true),
                    NumeroSeduteProscritte = table.Column<int>(type: "INTEGER", nullable: false),
                    ImportoTicket = table.Column<decimal>(type: "TEXT", nullable: true),
                    Stato = table.Column<string>(type: "TEXT", nullable: false),
                    AppuntoSegreteria = table.Column<string>(type: "TEXT", nullable: true),
                    IntegrazioneRichiestaIl = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ricette", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ricette_Pazienti_PazienteId",
                        column: x => x.PazienteId,
                        principalTable: "Pazienti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UtentiPazienti",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UtenteId = table.Column<string>(type: "TEXT", nullable: false),
                    PazienteId = table.Column<int>(type: "INTEGER", nullable: false),
                    Titolo = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UtentiPazienti", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UtentiPazienti_AspNetUsers_UtenteId",
                        column: x => x.UtenteId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UtentiPazienti_Pazienti_PazienteId",
                        column: x => x.PazienteId,
                        principalTable: "Pazienti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssenzeFisioterapisti",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FisioterapistaId = table.Column<int>(type: "INTEGER", nullable: false),
                    DataInizio = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DataFine = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Tipo = table.Column<string>(type: "TEXT", nullable: false),
                    StatoApprovazione = table.Column<string>(type: "TEXT", nullable: false),
                    Motivo = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssenzeFisioterapisti", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssenzeFisioterapisti_Fisioterapisti_FisioterapistaId",
                        column: x => x.FisioterapistaId,
                        principalTable: "Fisioterapisti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AcquistiPacchetto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PazienteId = table.Column<int>(type: "INTEGER", nullable: false),
                    Tipo = table.Column<string>(type: "TEXT", nullable: false),
                    SeduteTotali = table.Column<int>(type: "INTEGER", nullable: false),
                    SeduteResidue = table.Column<int>(type: "INTEGER", nullable: false),
                    Prezzo = table.Column<decimal>(type: "TEXT", nullable: true),
                    DataAcquisto = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Scadenza = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    RicettaId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcquistiPacchetto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcquistiPacchetto_Pazienti_PazienteId",
                        column: x => x.PazienteId,
                        principalTable: "Pazienti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AcquistiPacchetto_Ricette_RicettaId",
                        column: x => x.RicettaId,
                        principalTable: "Ricette",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Appuntamenti",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PazienteId = table.Column<int>(type: "INTEGER", nullable: false),
                    FisioterapistaId = table.Column<int>(type: "INTEGER", nullable: false),
                    DataOra = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DurataMinuti = table.Column<int>(type: "INTEGER", nullable: false),
                    Stato = table.Column<string>(type: "TEXT", nullable: false),
                    Percorso = table.Column<string>(type: "TEXT", nullable: false),
                    AcquistoPacchettoId = table.Column<int>(type: "INTEGER", nullable: true),
                    RicettaId = table.Column<int>(type: "INTEGER", nullable: true),
                    MotivoAnnullamento = table.Column<string>(type: "TEXT", nullable: true),
                    NotificaGiaDataAltrove = table.Column<bool>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appuntamenti", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Appuntamenti_AcquistiPacchetto_AcquistoPacchettoId",
                        column: x => x.AcquistoPacchettoId,
                        principalTable: "AcquistiPacchetto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appuntamenti_Fisioterapisti_FisioterapistaId",
                        column: x => x.FisioterapistaId,
                        principalTable: "Fisioterapisti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appuntamenti_Pazienti_PazienteId",
                        column: x => x.PazienteId,
                        principalTable: "Pazienti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Appuntamenti_Ricette_RicettaId",
                        column: x => x.RicettaId,
                        principalTable: "Ricette",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NoteSeduta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AppuntamentoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Testo = table.Column<string>(type: "TEXT", nullable: false),
                    RegistrataIl = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteSeduta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoteSeduta_Appuntamenti_AppuntamentoId",
                        column: x => x.AppuntamentoId,
                        principalTable: "Appuntamenti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notifiche",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tipo = table.Column<string>(type: "TEXT", nullable: false),
                    Canale = table.Column<string>(type: "TEXT", nullable: false),
                    Stato = table.Column<string>(type: "TEXT", nullable: false),
                    Destinatario = table.Column<string>(type: "TEXT", nullable: false),
                    Testo = table.Column<string>(type: "TEXT", nullable: false),
                    CreataIl = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PazienteId = table.Column<int>(type: "INTEGER", nullable: true),
                    AppuntamentoId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifiche", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifiche_Appuntamenti_AppuntamentoId",
                        column: x => x.AppuntamentoId,
                        principalTable: "Appuntamenti",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Notifiche_Pazienti_PazienteId",
                        column: x => x.PazienteId,
                        principalTable: "Pazienti",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Pagamenti",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PazienteId = table.Column<int>(type: "INTEGER", nullable: false),
                    Importo = table.Column<decimal>(type: "TEXT", nullable: false),
                    Metodo = table.Column<string>(type: "TEXT", nullable: false),
                    Stato = table.Column<string>(type: "TEXT", nullable: false),
                    Origine = table.Column<string>(type: "TEXT", nullable: false),
                    AcquistoPacchettoId = table.Column<int>(type: "INTEGER", nullable: true),
                    AppuntamentoId = table.Column<int>(type: "INTEGER", nullable: true),
                    RiferimentoStripe = table.Column<string>(type: "TEXT", nullable: true),
                    DataIncasso = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pagamenti", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pagamenti_AcquistiPacchetto_AcquistoPacchettoId",
                        column: x => x.AcquistoPacchettoId,
                        principalTable: "AcquistiPacchetto",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Pagamenti_Appuntamenti_AppuntamentoId",
                        column: x => x.AppuntamentoId,
                        principalTable: "Appuntamenti",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Pagamenti_Pazienti_PazienteId",
                        column: x => x.PazienteId,
                        principalTable: "Pazienti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProposteSlotAlternativo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AppuntamentoId = table.Column<int>(type: "INTEGER", nullable: false),
                    SlotProposti = table.Column<string>(type: "TEXT", nullable: false),
                    Scadenza = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Esito = table.Column<string>(type: "TEXT", nullable: false),
                    SlotScelto = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposteSlotAlternativo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProposteSlotAlternativo_Appuntamenti_AppuntamentoId",
                        column: x => x.AppuntamentoId,
                        principalTable: "Appuntamenti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ricevute",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NumeroProgressivo = table.Column<int>(type: "INTEGER", nullable: false),
                    Anno = table.Column<int>(type: "INTEGER", nullable: false),
                    PagamentoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Importo = table.Column<decimal>(type: "TEXT", nullable: false),
                    ImpostaBolloApplicata = table.Column<bool>(type: "INTEGER", nullable: false),
                    Stornata = table.Column<bool>(type: "INTEGER", nullable: false),
                    InviataSistemaTs = table.Column<bool>(type: "INTEGER", nullable: false),
                    OpposizioneSistemaTs = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ricevute", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ricevute_Pagamenti_PagamentoId",
                        column: x => x.PagamentoId,
                        principalTable: "Pagamenti",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcquistiPacchetto_PazienteId",
                table: "AcquistiPacchetto",
                column: "PazienteId");

            migrationBuilder.CreateIndex(
                name: "IX_AcquistiPacchetto_RicettaId",
                table: "AcquistiPacchetto",
                column: "RicettaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appuntamenti_AcquistoPacchettoId",
                table: "Appuntamenti",
                column: "AcquistoPacchettoId");

            migrationBuilder.CreateIndex(
                name: "IX_Appuntamenti_FisioterapistaId",
                table: "Appuntamenti",
                column: "FisioterapistaId");

            migrationBuilder.CreateIndex(
                name: "IX_Appuntamenti_PazienteId",
                table: "Appuntamenti",
                column: "PazienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Appuntamenti_RicettaId",
                table: "Appuntamenti",
                column: "RicettaId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssenzeFisioterapisti_FisioterapistaId",
                table: "AssenzeFisioterapisti",
                column: "FisioterapistaId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogAccessiClinici_PazienteId",
                table: "AuditLogAccessiClinici",
                column: "PazienteId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogAccessiClinici_UtenteId",
                table: "AuditLogAccessiClinici",
                column: "UtenteId");

            migrationBuilder.CreateIndex(
                name: "IX_CartelleCliniche_PazienteId",
                table: "CartelleCliniche",
                column: "PazienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Consensi_PazienteId",
                table: "Consensi",
                column: "PazienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Controindicazioni_PazienteId",
                table: "Controindicazioni",
                column: "PazienteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Fisioterapisti_UtenteId",
                table: "Fisioterapisti",
                column: "UtenteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NoteOperative_PazienteId",
                table: "NoteOperative",
                column: "PazienteId");

            migrationBuilder.CreateIndex(
                name: "IX_NoteSeduta_AppuntamentoId",
                table: "NoteSeduta",
                column: "AppuntamentoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifiche_AppuntamentoId",
                table: "Notifiche",
                column: "AppuntamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifiche_PazienteId",
                table: "Notifiche",
                column: "PazienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagamenti_AcquistoPacchettoId",
                table: "Pagamenti",
                column: "AcquistoPacchettoId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagamenti_AppuntamentoId",
                table: "Pagamenti",
                column: "AppuntamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagamenti_PazienteId",
                table: "Pagamenti",
                column: "PazienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Pazienti_CodiceFiscale",
                table: "Pazienti",
                column: "CodiceFiscale",
                unique: true,
                filter: "[CodiceFiscale] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProposteSlotAlternativo_AppuntamentoId",
                table: "ProposteSlotAlternativo",
                column: "AppuntamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Ricette_PazienteId",
                table: "Ricette",
                column: "PazienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Ricevute_Anno_NumeroProgressivo",
                table: "Ricevute",
                columns: new[] { "Anno", "NumeroProgressivo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ricevute_PagamentoId",
                table: "Ricevute",
                column: "PagamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_UtentiPazienti_PazienteId",
                table: "UtentiPazienti",
                column: "PazienteId");

            migrationBuilder.CreateIndex(
                name: "IX_UtentiPazienti_UtenteId_PazienteId",
                table: "UtentiPazienti",
                columns: new[] { "UtenteId", "PazienteId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "AssenzeFisioterapisti");

            migrationBuilder.DropTable(
                name: "AuditLogAccessiClinici");

            migrationBuilder.DropTable(
                name: "CartelleCliniche");

            migrationBuilder.DropTable(
                name: "ChiusureStudio");

            migrationBuilder.DropTable(
                name: "Consensi");

            migrationBuilder.DropTable(
                name: "Controindicazioni");

            migrationBuilder.DropTable(
                name: "ImpostazioniAgenda");

            migrationBuilder.DropTable(
                name: "ImpostazioniListino");

            migrationBuilder.DropTable(
                name: "NoteOperative");

            migrationBuilder.DropTable(
                name: "NoteSeduta");

            migrationBuilder.DropTable(
                name: "Notifiche");

            migrationBuilder.DropTable(
                name: "ProposteSlotAlternativo");

            migrationBuilder.DropTable(
                name: "Ricevute");

            migrationBuilder.DropTable(
                name: "UtentiPazienti");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "Pagamenti");

            migrationBuilder.DropTable(
                name: "Appuntamenti");

            migrationBuilder.DropTable(
                name: "AcquistiPacchetto");

            migrationBuilder.DropTable(
                name: "Fisioterapisti");

            migrationBuilder.DropTable(
                name: "Ricette");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Pazienti");
        }
    }
}
