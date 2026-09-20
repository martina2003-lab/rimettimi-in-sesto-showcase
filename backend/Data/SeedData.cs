using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RimettimiInSesto.Api.Models;

namespace RimettimiInSesto.Api.Data;

// Dati demo precaricati invece di un flusso di registrazione multi-utente — vedi
// ARCHITETTURA.md, "Note per il deploy demo". Nomi, date e stati raccontano la stessa
// storia coerente su tutti e quattro i ruoli, non generati a caso.
// Idempotente: non fa nulla se esiste già almeno un utente.
public static class SeedData
{
    public const string PasswordDemo = "demo1234";

    // "Oggi" dei mockup: lunedì 7 settembre 2026, il giorno su cui è ambientata la storia
    // raccontata dai quattro file di mockup. Le date qui sotto sono scritte come stanno lì,
    // e traslate in blocco sulla settimana corrente: quel che conta di questo seed sono le
    // *distanze* fra le date (il ciclo aperto prima delle sue sedute, la ricetta scaduta,
    // l'appuntamento ancora da fare), non i valori assoluti. Riscriverle a mano una per una
    // vorrebbe dire rifare a occhio quella coerenza, e sbagliarla.
    private static readonly DateOnly AncoraMockup = new(2026, 9, 7);

    private static readonly int ScartoGiorni = CalcolaScartoGiorni();

    private static int CalcolaScartoGiorni()
    {
        var oggi = DateTime.Today;
        var lunediCorrente = oggi.AddDays(oggi.DayOfWeek == DayOfWeek.Sunday ? -6 : 1 - (int)oggi.DayOfWeek);

        // Da mercoledì in poi la settimana in corso non basta: gli appuntamenti "futuri"
        // della storia (martedì, giovedì) sarebbero già passati e "In programma" resterebbe
        // vuoto — cioè il problema che questa traslazione esiste per risolvere.
        var target = oggi.DayOfWeek is DayOfWeek.Monday or DayOfWeek.Tuesday
            ? lunediCorrente
            : lunediCorrente.AddDays(7);

        // Multiplo di 7 per costruzione: lo studio è chiuso sabato e domenica, e uno scarto
        // a giorni sposterebbe l'appuntamento del giovedì su un giorno in cui il calcolo
        // disponibilità risponde "chiuso".
        return (target - AncoraMockup.ToDateTime(TimeOnly.MinValue)).Days;
    }

    /// <summary>Data e ora del mockup, riportata sulla settimana corrente.</summary>
    private static DateTime Data(int anno, int mese, int giorno, int ore = 0, int minuti = 0) =>
        new DateTime(anno, mese, giorno, ore, minuti, 0).AddDays(ScartoGiorni);

    /// <summary>Data del mockup, riportata sulla settimana corrente.</summary>
    private static DateOnly Giorno(int anno, int mese, int giorno) =>
        new DateOnly(anno, mese, giorno).AddDays(ScartoGiorni);

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        await db.Database.MigrateAsync();

        if (await userManager.Users.AnyAsync())
        {
            return; // già seedato
        }

        foreach (var ruolo in Enum.GetNames<Ruolo>())
        {
            if (!await roleManager.RoleExistsAsync(ruolo))
            {
                await roleManager.CreateAsync(new IdentityRole(ruolo));
            }
        }

        // ---------- Utenti con login (i quattro account demo, uno per ruolo) ----------
        var francesca = await CreaUtente(userManager, "francesca@studio.example", "Francesca", "", Ruolo.Coordinatrice);
        var fabrizio = await CreaUtente(userManager, "fabrizio.rinaldi@studio.example", "Fabrizio", "Rinaldi", Ruolo.Admin);
        var elenaUtente = await CreaUtente(userManager, "elena.ricci@studio.example", "Elena", "Ricci", Ruolo.Fisioterapista);
        var marcoUtente = await CreaUtente(userManager, "marco.bianchi@paziente.example", "Marco", "Bianchi", Ruolo.Paziente);

        // Fisioterapisti aggiuntivi (nessun account demo dedicato in login.html, ma reali nei mockup).
        var andreaUtente = await CreaUtente(userManager, "andrea.conti@studio.example", "Andrea", "Conti", Ruolo.Fisioterapista);
        var saraUtente = await CreaUtente(userManager, "sara.moretti@studio.example", "Sara", "Moretti", Ruolo.Fisioterapista);
        var lucaUtente = await CreaUtente(userManager, "luca.fabbri@studio.example", "Luca", "Fabbri", Ruolo.Fisioterapista);

        // ---------- Profili Fisioterapista ----------
        // Le bio sono quello che il paziente legge mentre sceglie: senza, il pannello
        // "Chi è" del wizard resta vuoto. Quella di Elena Ricci è la stessa già scritta
        // in fisioterapista.html, dove è lei a gestirla.
        var ricci = new Fisioterapista
        {
            UtenteId = elenaUtente.Id,
            TipoContratto = TipoContratto.Dipendente,
            OreSettimanaliContratto = 38m,
            PatternDisponibilita = "Lun-Ven, mattina e pomeriggio (Mer solo mattina)",
            Bio = "Laureata in Fisioterapia all'Università \"Tor Vergata\", lavora da oltre dieci anni " +
                  "nella riabilitazione ortopedica post-chirurgica e nella rieducazione posturale. Si è " +
                  "formata anche in terapia manuale e Kinesio Taping, e segue con particolare attenzione " +
                  "i percorsi di recupero dopo interventi a ginocchio e spalla.",
        };
        var conti = new Fisioterapista
        {
            UtenteId = andreaUtente.Id,
            TipoContratto = TipoContratto.Dipendente,
            OreSettimanaliContratto = 38m,
            PatternDisponibilita = "Lun-Ven, alternanza mattina/pomeriggio",
            Bio = "Fisioterapista dal 2014, si occupa soprattutto di riabilitazione neuromotoria e di " +
                  "rieducazione del cammino. Collabora stabilmente con il fisiatra dello studio nei " +
                  "percorsi più lunghi e nei casi con più distretti coinvolti.",
        };
        var moretti = new Fisioterapista
        {
            UtenteId = saraUtente.Id,
            TipoContratto = TipoContratto.Collaboratore,
            OreSettimanaliContratto = 24m,
            PatternDisponibilita = "Lun-Ven 9-13, Mar e Gio anche 15-19",
            Bio = "Specializzata in riabilitazione del pavimento pelvico e in ginnastica posturale, " +
                  "segue percorsi pre e post parto. Lavora molto con la terapia manuale e dedica la " +
                  "prima seduta a una valutazione funzionale approfondita.",
        };
        var fabbri = new Fisioterapista
        {
            UtenteId = lucaUtente.Id,
            TipoContratto = TipoContratto.Collaboratore,
            OreSettimanaliContratto = 24m,
            PatternDisponibilita = "Lun, Mer, Ven 9-13 e 15-19",
            Bio = "Si occupa di riabilitazione ortopedica e sportiva, con particolare esperienza negli " +
                  "esiti di frattura e nel recupero dell'articolarità di polso, mano e caviglia. Segue " +
                  "anche i pazienti più giovani, dove il percorso va spiegato prima di tutto a loro.",
        };
        db.Fisioterapisti.AddRange(ricci, conti, moretti, fabbri);
        await db.SaveChangesAsync();

        // Assenza di Sara Moretti, 8-15 settembre 2026 (visibile in griglia in coordinatrice.html).
        db.AssenzeFisioterapisti.Add(new AssenzaFisioterapista
        {
            FisioterapistaId = moretti.Id,
            DataInizio = Giorno(2026, 9, 8),
            DataFine = Giorno(2026, 9, 15),
            Tipo = TipoAssenza.Pianificata,
            StatoApprovazione = StatoApprovazioneAssenza.Approvata,
            Motivo = "Ferie",
        });

        // ---------- Pazienti ----------
        var marco = new Paziente
        {
            Nome = "Marco", Cognome = "Bianchi", CodiceFiscale = "BNCMRC80A01H501X",
            Telefono = "333 1234567", Email = "marco.bianchi@paziente.example",
            Stato = StatoPaziente.ConsensoRaccolto,
        };
        var giulia = new Paziente
        {
            Nome = "Giulia", Cognome = "Bianchi", CodiceFiscale = "BNCGLI14A41H501Y",
            Telefono = "333 1234567", Stato = StatoPaziente.ConsensoRaccolto,
            // 1 gennaio 2014 dal codice fiscale (giorno "41" = 1 + 40, il +40 marca il sesso femminile).
            // La data di nascita non si trasla: deve restare coerente col codice fiscale.
            DataNascita = new DateOnly(2014, 1, 1),
        };
        var paolo = new Paziente
        {
            Nome = "Paolo", Cognome = "Ferri", CodiceFiscale = "FRRPLA75B12H501Z",
            Telefono = "328 7654321", Stato = StatoPaziente.ConsensoRaccolto,
        };
        var anna = new Paziente
        {
            Nome = "Anna", Cognome = "Colombo", CodiceFiscale = "CLMNNA90C55H501W",
            Telefono = "347 1122334", Stato = StatoPaziente.ConsensoRaccolto,
        };
        var roberto = new Paziente
        {
            Nome = "Roberto", Cognome = "Greco", CodiceFiscale = null,
            Telefono = "339 9988776", Stato = StatoPaziente.Provvisorio,
        };
        var teresa = new Paziente
        {
            Nome = "Teresa", Cognome = "Amato", CodiceFiscale = "MTATRS58D62H501K",
            Telefono = "340 4455667", Stato = StatoPaziente.ConsensoRaccolto,
        };
        db.Pazienti.AddRange(marco, giulia, paolo, anna, roberto, teresa);
        await db.SaveChangesAsync();

        // Marco gestisce anche Giulia (figlia minorenne) — switcher familiare di paziente.html.
        db.UtentiPazienti.AddRange(
            new UtentePaziente { UtenteId = marcoUtente.Id, PazienteId = marco.Id, Titolo = TitoloRelazione.SeStesso },
            new UtentePaziente { UtenteId = marcoUtente.Id, PazienteId = giulia.Id, Titolo = TitoloRelazione.Genitore }
        );

        db.NoteOperative.Add(new NotaOperativa
        {
            PazienteId = marco.Id, Testo = "Preferisce il tardo pomeriggio.",
            Autore = "Francesca", Data = Data(2026, 8, 20),
        });
        db.NoteOperative.Add(new NotaOperativa
        {
            PazienteId = roberto.Id,
            Testo = "Scheda creata per bloccare lo slot al telefono. Consenso da raccogliere alla prima seduta.",
            Autore = "Francesca", Data = Data(2026, 9, 6),
        });
        db.NoteOperative.Add(new NotaOperativa
        {
            PazienteId = paolo.Id, Testo = "Chiamare per confermare il numero della ricetta, foto poco leggibile.",
            Autore = "Francesca", Data = Data(2026, 9, 5),
        });

        // ---------- Ricette SSN ----------
        var ricettaGiulia = new Ricetta
        {
            PazienteId = giulia.Id, Stato = StatoRicetta.Validata,
            NumeroONre = "120A44718802391", MedicoPrescrittore = "Dott.ssa Marina Villa",
            DataEmissione = Giorno(2026, 8, 27), Scadenza = Giorno(2026, 11, 27),
            FinestraCompletamento = Giorno(2026, 10, 12),
            CodicePrestazioneBranca = "93 — Medicina fisica e riabilitativa",
            DistrettiCorporei = "Arto superiore destro",
            QuesitoDiagnostico = "Rigidità e limitazione funzionale del polso destro in esiti di frattura consolidata da caduta in bicicletta",
            NumeroSeduteProscritte = 10, ImportoTicket = 36.00m,
        };
        var ricettaPaolo = new Ricetta
        {
            PazienteId = paolo.Id, Stato = StatoRicetta.NonAncoraValidata,
            NumeroONre = "120A44719003118", MedicoPrescrittore = "Dott. Giovanni Esposito",
            DataEmissione = Giorno(2026, 9, 2), Scadenza = Giorno(2026, 12, 2),
            CodicePrestazioneBranca = "93 — Medicina fisica e riabilitativa",
            DistrettiCorporei = "Arto superiore destro",
            QuesitoDiagnostico = "Periartrite scapolo-omerale destra post-traumatica",
            NumeroSeduteProscritte = 10, ImportoTicket = 36.00m,
            AppuntoSegreteria = "Chiamare per confermare il numero della ricetta, foto poco leggibile.",
        };
        var ricettaTeresa = new Ricetta
        {
            // Caso limite deliberato (coordinatrice.html): 12 sedute prescritte oltre il tetto
            // regionale, CF su ricetta non combacia con la scheda — i controlli segnalano, non decidono.
            PazienteId = teresa.Id, Stato = StatoRicetta.NonAncoraValidata,
            NumeroONre = "120A44715519024", MedicoPrescrittore = "Dott. Sergio Pini",
            DataEmissione = Giorno(2026, 5, 14), Scadenza = Giorno(2026, 8, 14),
            CodicePrestazioneBranca = "93 — Medicina fisica e riabilitativa",
            DistrettiCorporei = "Colonna cervicale, Arto superiore sinistro",
            QuesitoDiagnostico = "Esiti di intervento per cuffia dei rotatori sinistra",
            NumeroSeduteProscritte = 12, Esenzione = true, CodiceEsenzione = "C01", ImportoTicket = 0m,
        };
        db.Ricette.AddRange(ricettaGiulia, ricettaPaolo, ricettaTeresa);
        await db.SaveChangesAsync();

        // ---------- Pacchetti privati / cicli SSN ----------
        var pacchettoMarco = new AcquistoPacchetto
        {
            PazienteId = marco.Id, Tipo = TipoPacchetto.Privato,
            SeduteTotali = 10, SeduteResidue = 6, Prezzo = 380m,
            DataAcquisto = Giorno(2026, 9, 7), Scadenza = Giorno(2027, 9, 7),
        };
        var pacchettoAnna = new AcquistoPacchetto
        {
            PazienteId = anna.Id, Tipo = TipoPacchetto.Privato,
            SeduteTotali = 10, SeduteResidue = 2, Prezzo = 380m,
            DataAcquisto = Giorno(2026, 1, 2), Scadenza = Giorno(2027, 1, 2),
        };
        var cicloGiulia = new AcquistoPacchetto
        {
            PazienteId = giulia.Id, Tipo = TipoPacchetto.Ssn,
            SeduteTotali = 10, SeduteResidue = 6,
            DataAcquisto = ricettaGiulia.DataEmissione, Scadenza = ricettaGiulia.FinestraCompletamento,
            RicettaId = ricettaGiulia.Id,
        };
        db.AcquistiPacchetto.AddRange(pacchettoMarco, pacchettoAnna, cicloGiulia);
        await db.SaveChangesAsync();

        // ---------- Appuntamenti (storico di coordinatrice.html, presa come fonte di verità) ----------
        var appMarco8Set = new Appuntamento
        {
            PazienteId = marco.Id, FisioterapistaId = ricci.Id,
            DataOra = Data(2026, 9, 8, 15, 30), DurataMinuti = 60,
            Stato = StatoAppuntamento.Confermato, Percorso = PercorsoAppuntamento.Privato,
            AcquistoPacchettoId = pacchettoMarco.Id,
        };
        var appMarco1Set = new Appuntamento
        {
            PazienteId = marco.Id, FisioterapistaId = ricci.Id,
            DataOra = Data(2026, 9, 1, 15, 30), DurataMinuti = 60,
            Stato = StatoAppuntamento.Completato, Percorso = PercorsoAppuntamento.Privato,
            AcquistoPacchettoId = pacchettoMarco.Id,
        };
        var appGiulia7Set = new Appuntamento
        {
            PazienteId = giulia.Id, FisioterapistaId = fabbri.Id,
            DataOra = Data(2026, 9, 7, 9, 0), DurataMinuti = 30,
            Stato = StatoAppuntamento.Confermato, Percorso = PercorsoAppuntamento.Ssn,
            AcquistoPacchettoId = cicloGiulia.Id, RicettaId = ricettaGiulia.Id,
        };
        var appPaolo7Set = new Appuntamento
        {
            PazienteId = paolo.Id, FisioterapistaId = ricci.Id,
            DataOra = Data(2026, 9, 7, 10, 0), DurataMinuti = 30,
            Stato = StatoAppuntamento.Richiesto, Percorso = PercorsoAppuntamento.Ssn,
            RicettaId = ricettaPaolo.Id,
        };
        var appAnna7SetConti = new Appuntamento
        {
            PazienteId = anna.Id, FisioterapistaId = conti.Id,
            DataOra = Data(2026, 9, 7, 11, 0), DurataMinuti = 60,
            Stato = StatoAppuntamento.Confermato, Percorso = PercorsoAppuntamento.Privato,
            AcquistoPacchettoId = pacchettoAnna.Id,
        };
        var appAnna10SetRicci = new Appuntamento
        {
            // Deliberato: Anna sceglie Ricci pur avendo Conti come "abituale" — nessun legame fisso.
            PazienteId = anna.Id, FisioterapistaId = ricci.Id,
            DataOra = Data(2026, 9, 10, 11, 0), DurataMinuti = 60,
            Stato = StatoAppuntamento.Confermato, Percorso = PercorsoAppuntamento.Privato,
            AcquistoPacchettoId = pacchettoAnna.Id,
        };
        var appMarco19AgoFuoriPacchetto = new Appuntamento
        {
            PazienteId = marco.Id, FisioterapistaId = ricci.Id,
            DataOra = Data(2026, 8, 19, 16, 0), DurataMinuti = 60,
            Stato = StatoAppuntamento.Completato, Percorso = PercorsoAppuntamento.Privato,
            // Fuori pacchetto per scelta: genera la voce da saldare c6, non collegata ad AcquistoPacchetto.
        };
        db.Appuntamenti.AddRange(
            appMarco8Set, appMarco1Set, appGiulia7Set, appPaolo7Set,
            appAnna7SetConti, appAnna10SetRicci, appMarco19AgoFuoriPacchetto);
        await db.SaveChangesAsync();

        // ---------- Consensi ----------
        // Come in paziente.html: Marco firma per sé, per Giulia firma il padre — lei è
        // minorenne, e i moduli cartacei chiedono espressamente chi esercita la potestà.
        // Roberto Greco non ne ha nessuno: è la scheda provvisoria nata al telefono, e il
        // consenso si raccoglie di persona alla prima seduta.
        foreach (var tipo in Enum.GetValues<TipoConsenso>())
        {
            db.Consensi.Add(new Consenso
            {
                PazienteId = marco.Id, Tipo = tipo, Data = Data(2026, 8, 18),
                VersioneInformativa = "v2.1", Stato = StatoConsenso.Prestato,
                Firmatario = "Marco Bianchi",
            });
            db.Consensi.Add(new Consenso
            {
                PazienteId = giulia.Id, Tipo = tipo, Data = Data(2026, 8, 10),
                VersioneInformativa = "v2.1", Stato = StatoConsenso.Prestato,
                Firmatario = "Marco Bianchi (genitore)",
            });
            foreach (var altro in new[] { paolo, anna, teresa })
            {
                db.Consensi.Add(new Consenso
                {
                    PazienteId = altro.Id, Tipo = tipo, Data = Data(2026, 8, 18),
                    VersioneInformativa = "v2.1", Stato = StatoConsenso.Prestato,
                    Firmatario = $"{altro.Nome} {altro.Cognome}",
                });
            }
        }
        await db.SaveChangesAsync();

        // ---------- Lista d'attesa ----------
        // Anna Colombo aspetta di essere spostata prima, come in coordinatrice.html: ha già
        // il suo appuntamento confermato con Conti — l'avviso è un desiderio in più, non
        // l'assenza di una prenotazione.
        db.AvvisiDisponibilita.Add(new AvvisoDisponibilita
        {
            PazienteId = anna.Id,
            AppuntamentoId = appAnna7SetConti.Id,
            CreatoIl = Data(2026, 9, 3, 9, 30),
            Stato = StatoAvvisoDisponibilita.Attivo,
        });
        await db.SaveChangesAsync();

        // ---------- Cartella clinica ----------
        // Senza almeno un ciclo aperto, la cartella del paziente e quella del fisioterapista
        // si presentano vuote, e la parte più delicata del progetto sembra non funzionare.
        // Diagnosi e riepiloghi sono quelli già scritti in fisioterapista.html.
        db.CartelleCliniche.AddRange(
            new CartellaClinica
            {
                PazienteId = marco.Id,
                Diagnosi = "Lombalgia cronica meccanico-posturale su base degenerativa discale L4-L5.",
                AnamnesiPatologicaRemota = "Episodi ricorrenti di lombalgia dal 2019, mai indagati con imaging fino al 2026.",
                EsameObiettivo = "Contrattura paravertebrale bilaterale, limitazione in flessione anteriore, Lasègue negativo.",
                EsamiSpecialistici = "RM lombosacrale (07/2026): protrusione discale L4-L5 senza conflitto radicolare.",
                ProgrammaRiabilitativo = "Terapia manuale, rieducazione posturale e progressivo rinforzo del core.",
                IndicazioniPaziente = "Evitare il sollevamento di carichi a schiena flessa; esercizi domiciliari quotidiani.",
                VasIniziale = 7,
                DataInizioTerapia = Giorno(2026, 8, 18),
                FirmaFisioterapista = "Dott.ssa Elena Ricci",
                FirmaMedicoResponsabile = "Dott. Riccardo Fatarella",
            },
            new CartellaClinica
            {
                PazienteId = giulia.Id,
                Diagnosi = "Esiti di frattura del radio distale destro, consolidata, con rigidità articolare residua.",
                AnamnesiPatologicaRemota = "Caduta in bicicletta a giugno 2026, trattamento conservativo con gesso per 5 settimane.",
                EsameObiettivo = "Limitazione in flesso-estensione del polso destro, forza di presa ridotta rispetto al controlaterale.",
                ProgrammaRiabilitativo = "Mobilizzazione progressiva del polso, recupero della forza di presa, ultrasuonoterapia.",
                IndicazioniPaziente = "Esercizi di mobilizzazione due volte al giorno, senza forzare il dolore.",
                VasIniziale = 4,
                DataInizioTerapia = Giorno(2026, 8, 28),
                FirmaFisioterapista = "Dott. Luca Fabbri",
                FirmaMedicoResponsabile = "Dott. Riccardo Fatarella",
            });

        // Riepiloghi delle sedute già completate: è quello che il paziente legge nel proprio
        // portale, ed è l'unico elemento della cartella per singola seduta.
        db.NoteSeduta.AddRange(
            new NotaSeduta
            {
                AppuntamentoId = appMarco19AgoFuoriPacchetto.Id,
                Testo = "Prima seduta di terapia manuale. Paziente teso, si consiglia di introdurre " +
                        "gradualmente gli esercizi attivi dalla prossima seduta.",
                RegistrataIl = Data(2026, 8, 19, 17, 0),
            },
            new NotaSeduta
            {
                AppuntamentoId = appMarco1Set.Id,
                Testo = "Lavoro su mobilità lombare e core stability. Buona tolleranza, dolore in " +
                        "lieve calo (VAS riferito 5/10 a fine seduta).",
                RegistrataIl = Data(2026, 9, 1, 16, 30),
            });

        // ---------- Cassa: pagamenti (c1-c6 di coordinatrice.html) ----------
        db.Pagamenti.AddRange(
            new Pagamento
            {
                PazienteId = marco.Id, Importo = 380m, Metodo = MetodoPagamento.InStudio,
                Stato = StatoPagamento.Pagato, Origine = "Acquisto pacchetto 10 sedute",
                // Incassati oggi: è la giornata che la Coordinatrice si trova in cassa
                // aprendo il portale, e con una data futura la quadratura non avrebbe senso.
                AcquistoPacchettoId = pacchettoMarco.Id, DataIncasso = DateTime.Today,
            },
            new Pagamento
            {
                PazienteId = giulia.Id, Importo = 36m, Metodo = MetodoPagamento.Online,
                Stato = StatoPagamento.Pagato, Origine = "Ticket ciclo SSN — dovuto una volta per ciclo",
                DataIncasso = DateTime.Today,
                // Collegato al ciclo che lo ha generato: senza, nelle entrate dell'Admin
                // il percorso non è derivabile e un ticket SSN finisce fra i privati.
                AcquistoPacchettoId = cicloGiulia.Id,
            },
            new Pagamento
            {
                PazienteId = anna.Id, Importo = 60m, Metodo = MetodoPagamento.InStudio,
                Stato = StatoPagamento.DaSaldare,
                Origine = $"Seduta singola — terapia manuale 60 min (appuntamento {appAnna7SetConti.DataOra:dd/MM/yyyy}, {appAnna7SetConti.DataOra:HH:mm}, Dott. Andrea Conti)",
                AppuntamentoId = appAnna7SetConti.Id,
            },
            new Pagamento
            {
                PazienteId = teresa.Id, Importo = 0m, Metodo = MetodoPagamento.TicketSsn,
                Stato = StatoPagamento.Pagato, Origine = "Ticket ciclo SSN — esenzione C01, invalidità civile 100%",
            },
            new Pagamento
            {
                PazienteId = anna.Id, Importo = 90m, Metodo = MetodoPagamento.InStudio,
                Stato = StatoPagamento.DaSaldare, Origine = "Ciclo tecarterapia — 3 applicazioni (percorso privato, richieste dal paziente)",
                TipoPrestazione = TipoPrestazione.Strumentale,
            },
            new Pagamento
            {
                PazienteId = marco.Id, Importo = 55m, Metodo = MetodoPagamento.InStudio,
                Stato = StatoPagamento.DaSaldare,
                Origine = $"Seduta singola fuori pacchetto — recupero del {appMarco19AgoFuoriPacchetto.DataOra:dd/MM/yyyy}, Dott.ssa Elena Ricci",
                AppuntamentoId = appMarco19AgoFuoriPacchetto.Id,
            }
        );
        await db.SaveChangesAsync();

        var pagamentoMarco = await db.Pagamenti.FirstAsync(p => p.PazienteId == marco.Id && p.Importo == 380m);
        var pagamentoGiulia = await db.Pagamenti.FirstAsync(p => p.PazienteId == giulia.Id && p.Importo == 36m);
        var pagamentoAnnaValutazione = await db.Pagamenti.AddAsync(new Pagamento
        {
            PazienteId = anna.Id, Importo = 70m, Metodo = MetodoPagamento.InStudio,
            Stato = StatoPagamento.Pagato, Origine = "Valutazione funzionale iniziale", DataIncasso = DateTime.Today.AddDays(-4),
        });

        // ---------- Storico di cassa dei due mesi precedenti ----------
        // Senza questo storico, "Andamento" dell'Admin confronta sempre lo stesso caso: da
        // zero a qualcosa. Le date sono ancorate al primo giorno del mese vero (non a
        // DateTime.Today - N mesi, che scivolerebbe di mese vicino ai bordi del calendario)
        // così ogni voce cade sempre nel mese giusto, indipendentemente da che giorno è oggi.
        var inizioMeseScorso = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1);
        var inizioDueMesiFa = inizioMeseScorso.AddMonths(-1);

        var pagamentoMarcoSedutaPrimaDelPacchetto = await db.Pagamenti.AddAsync(new Pagamento
        {
            PazienteId = marco.Id, Importo = 60m, Metodo = MetodoPagamento.InStudio,
            Stato = StatoPagamento.Pagato, Origine = "Seduta singola — terapia manuale 60 min, prima dell'acquisto del pacchetto",
            DataIncasso = inizioDueMesiFa.AddDays(4),
        });
        var pagamentoAnnaTecarPrecedente = await db.Pagamenti.AddAsync(new Pagamento
        {
            PazienteId = anna.Id, Importo = 90m, Metodo = MetodoPagamento.InStudio,
            Stato = StatoPagamento.Pagato, Origine = "Ciclo tecarterapia — 3 applicazioni (percorso privato, richieste dal paziente)",
            TipoPrestazione = TipoPrestazione.Strumentale, DataIncasso = inizioDueMesiFa.AddDays(12),
        });
        var pagamentoPaoloTicketPrecedente = await db.Pagamenti.AddAsync(new Pagamento
        {
            PazienteId = paolo.Id, Importo = 36m, Metodo = MetodoPagamento.TicketSsn,
            Stato = StatoPagamento.Pagato, Origine = "Ticket ciclo SSN — dovuto una volta per ciclo",
            DataIncasso = inizioMeseScorso.AddDays(7),
        });
        var pagamentoRobertoPrimaSeduta = await db.Pagamenti.AddAsync(new Pagamento
        {
            PazienteId = roberto.Id, Importo = 60m, Metodo = MetodoPagamento.InStudio,
            Stato = StatoPagamento.Pagato, Origine = "Seduta singola — terapia manuale 60 min, walk-in",
            DataIncasso = inizioMeseScorso.AddDays(13),
        });
        var pagamentoAnnaSedutaManuale = await db.Pagamenti.AddAsync(new Pagamento
        {
            PazienteId = anna.Id, Importo = 60m, Metodo = MetodoPagamento.InStudio,
            Stato = StatoPagamento.Pagato, Origine = "Seduta singola — terapia manuale 60 min",
            DataIncasso = inizioMeseScorso.AddDays(20),
        });
        await db.SaveChangesAsync();

        // ---------- Storico 2025, per il confronto anno su anno di "Andamento" ----------
        // Senza questo, ogni confronto con l'anno prima è una divisione per zero (il 2025
        // era vuoto nel seed) e "Andamento" può solo tacere sul punto, come faceva finora.
        // Stessi tre mesi reali già popolati nel 2026 (due mesi fa / mese scorso / mese
        // corrente), un anno prima: il confronto resta onesto, non tre mesi a caso.
        // Importi più bassi di quelli del 2026, per raccontare una crescita reale — ma non
        // così bassi da rendere il mese corrente (parziale, ancora in corso) peggiore del
        // 2025 già chiuso: un dashboard che segnala un calo mai avvenuto è il difetto che
        // questo stesso file ha già dovuto correggere una volta (vedi "barre future" sopra).
        // Agganciate a pazienti già esistenti nel seed: l'Admin non mostra mai nomi di
        // pazienti, quindi qui contano solo come cifra, non come identità.
        var inizioMeseCorrente = inizioMeseScorso.AddMonths(1);
        db.Pagamenti.AddRange(
            new Pagamento
            {
                PazienteId = roberto.Id, Importo = 90m, Metodo = MetodoPagamento.InStudio,
                Stato = StatoPagamento.Pagato, Origine = "Seduta singola — terapia manuale 60 min",
                DataIncasso = inizioDueMesiFa.AddYears(-1).AddDays(9),
            },
            new Pagamento
            {
                PazienteId = paolo.Id, Importo = 5m, Metodo = MetodoPagamento.TicketSsn,
                Stato = StatoPagamento.Pagato, Origine = "Ticket ciclo SSN — dovuto una volta per ciclo",
                DataIncasso = inizioDueMesiFa.AddYears(-1).AddDays(16),
            },
            new Pagamento
            {
                PazienteId = anna.Id, Importo = 215m, Metodo = MetodoPagamento.InStudio,
                Stato = StatoPagamento.Pagato, Origine = "Sedute singole — terapia manuale",
                DataIncasso = inizioMeseScorso.AddYears(-1).AddDays(6),
            },
            new Pagamento
            {
                PazienteId = paolo.Id, Importo = 15m, Metodo = MetodoPagamento.TicketSsn,
                Stato = StatoPagamento.Pagato, Origine = "Ticket ciclo SSN — dovuto una volta per ciclo",
                DataIncasso = inizioMeseScorso.AddYears(-1).AddDays(19),
            },
            new Pagamento
            {
                PazienteId = roberto.Id, Importo = 395m, Metodo = MetodoPagamento.InStudio,
                Stato = StatoPagamento.Pagato, Origine = "Sedute singole — terapia manuale",
                DataIncasso = inizioMeseCorrente.AddYears(-1).AddDays(8),
            },
            new Pagamento
            {
                PazienteId = paolo.Id, Importo = 25m, Metodo = MetodoPagamento.TicketSsn,
                Stato = StatoPagamento.Pagato, Origine = "Ticket ciclo SSN — dovuto una volta per ciclo",
                DataIncasso = inizioMeseCorrente.AddYears(-1).AddDays(21),
            }
        );
        await db.SaveChangesAsync();

        // ---------- Storico delle ultime settimane, per la vista Andamento a grana
        // settimanale/mensile dell'Admin ----------
        // Senza queste, "Settimana" e "Mese" avrebbero al più un giorno con incassi: la
        // stessa sparsità che rendeva piatto il grafico mensile, spostata di livello. Quattro
        // pazienti nuovi (mai visti nella cartella clinica: qui contano solo come cassa),
        // con sedute sparse sulle ultime tre settimane vere.
        var chiara = new Paziente
        {
            Nome = "Chiara", Cognome = "Longo", CodiceFiscale = "LNGCHR88E44H501P",
            Telefono = "347 6655443", Stato = StatoPaziente.ConsensoRaccolto,
        };
        var davide = new Paziente
        {
            Nome = "Davide", Cognome = "Ferretti", CodiceFiscale = "FRRDVD82L15H501Q",
            Telefono = "339 2233445", Stato = StatoPaziente.ConsensoRaccolto,
        };
        var matteo = new Paziente
        {
            Nome = "Matteo", Cognome = "Bruni", CodiceFiscale = "BRNMTT91S20H501R",
            Telefono = "328 5566778", Stato = StatoPaziente.ConsensoRaccolto,
        };
        var silvia = new Paziente
        {
            Nome = "Silvia", Cognome = "Marino", CodiceFiscale = "MRNSLV95M56H501S",
            Telefono = "333 8899001", Stato = StatoPaziente.ConsensoRaccolto,
        };
        db.Pazienti.AddRange(chiara, davide, matteo, silvia);
        await db.SaveChangesAsync();

        // Lunedì della settimana in corso, calcolato come CalcolaScartoGiorni(): a differenza
        // delle date di agenda, gli incassi non seguono lo scarto dei mockup (vedi nota sopra
        // su DataIncasso = DateTime.Today) — servono settimane vere del calendario reale.
        var oggiCassa = DateTime.Today;
        var lunediSettimanaCorrente = oggiCassa.AddDays(oggiCassa.DayOfWeek == DayOfWeek.Sunday ? -6 : 1 - (int)oggiCassa.DayOfWeek);

        var appChiara1 = new Appuntamento
        {
            PazienteId = chiara.Id, FisioterapistaId = ricci.Id,
            DataOra = lunediSettimanaCorrente.AddDays(-20).Date.AddHours(10), DurataMinuti = 60,
            Stato = StatoAppuntamento.Completato, Percorso = PercorsoAppuntamento.Privato,
        };
        var appDavide1 = new Appuntamento
        {
            PazienteId = davide.Id, FisioterapistaId = conti.Id,
            DataOra = lunediSettimanaCorrente.AddDays(-18).Date.AddHours(16).AddMinutes(30), DurataMinuti = 60,
            Stato = StatoAppuntamento.Completato, Percorso = PercorsoAppuntamento.Privato,
        };
        var appMatteo1 = new Appuntamento
        {
            PazienteId = matteo.Id, FisioterapistaId = moretti.Id,
            DataOra = lunediSettimanaCorrente.AddDays(-14).Date.AddHours(11), DurataMinuti = 60,
            Stato = StatoAppuntamento.Completato, Percorso = PercorsoAppuntamento.Privato,
        };
        var appChiara2 = new Appuntamento
        {
            PazienteId = chiara.Id, FisioterapistaId = ricci.Id,
            DataOra = lunediSettimanaCorrente.AddDays(-10).Date.AddHours(17), DurataMinuti = 60,
            Stato = StatoAppuntamento.Completato, Percorso = PercorsoAppuntamento.Privato,
        };
        var appSilvia1 = new Appuntamento
        {
            PazienteId = silvia.Id, FisioterapistaId = fabbri.Id,
            DataOra = lunediSettimanaCorrente.AddDays(-5).Date.AddHours(9).AddMinutes(30), DurataMinuti = 60,
            Stato = StatoAppuntamento.Completato, Percorso = PercorsoAppuntamento.Privato,
        };
        var appDavide2 = new Appuntamento
        {
            PazienteId = davide.Id, FisioterapistaId = conti.Id,
            DataOra = lunediSettimanaCorrente.AddDays(-3).Date.AddHours(15).AddMinutes(30), DurataMinuti = 60,
            Stato = StatoAppuntamento.Completato, Percorso = PercorsoAppuntamento.Privato,
        };
        db.Appuntamenti.AddRange(appChiara1, appDavide1, appMatteo1, appChiara2, appSilvia1, appDavide2);
        await db.SaveChangesAsync();

        var pagamentoChiara1 = await db.Pagamenti.AddAsync(new Pagamento
        {
            PazienteId = chiara.Id, Importo = 60m, Metodo = MetodoPagamento.InStudio,
            Stato = StatoPagamento.Pagato, AppuntamentoId = appChiara1.Id,
            Origine = $"Seduta singola — terapia manuale 60 min (appuntamento {appChiara1.DataOra:dd/MM/yyyy}, {appChiara1.DataOra:HH:mm}, Dott.ssa Elena Ricci)",
            DataIncasso = appChiara1.DataOra.Date,
        });
        var pagamentoDavide1 = await db.Pagamenti.AddAsync(new Pagamento
        {
            PazienteId = davide.Id, Importo = 60m, Metodo = MetodoPagamento.InStudio,
            Stato = StatoPagamento.Pagato, AppuntamentoId = appDavide1.Id,
            Origine = $"Seduta singola — terapia manuale 60 min (appuntamento {appDavide1.DataOra:dd/MM/yyyy}, {appDavide1.DataOra:HH:mm}, Dott. Andrea Conti)",
            DataIncasso = appDavide1.DataOra.Date,
        });
        var pagamentoMatteo1 = await db.Pagamenti.AddAsync(new Pagamento
        {
            PazienteId = matteo.Id, Importo = 55m, Metodo = MetodoPagamento.InStudio,
            Stato = StatoPagamento.Pagato, AppuntamentoId = appMatteo1.Id,
            Origine = $"Seduta singola — terapia manuale 60 min (appuntamento {appMatteo1.DataOra:dd/MM/yyyy}, {appMatteo1.DataOra:HH:mm}, Dott.ssa Sara Moretti)",
            DataIncasso = appMatteo1.DataOra.Date,
        });
        var pagamentoChiara2 = await db.Pagamenti.AddAsync(new Pagamento
        {
            PazienteId = chiara.Id, Importo = 60m, Metodo = MetodoPagamento.InStudio,
            Stato = StatoPagamento.Pagato, AppuntamentoId = appChiara2.Id,
            Origine = $"Seduta singola — terapia manuale 60 min (appuntamento {appChiara2.DataOra:dd/MM/yyyy}, {appChiara2.DataOra:HH:mm}, Dott.ssa Elena Ricci)",
            DataIncasso = appChiara2.DataOra.Date,
        });
        var pagamentoSilvia1 = await db.Pagamenti.AddAsync(new Pagamento
        {
            PazienteId = silvia.Id, Importo = 60m, Metodo = MetodoPagamento.InStudio,
            Stato = StatoPagamento.Pagato, AppuntamentoId = appSilvia1.Id,
            Origine = $"Seduta singola — terapia manuale 60 min (appuntamento {appSilvia1.DataOra:dd/MM/yyyy}, {appSilvia1.DataOra:HH:mm}, Dott. Luca Fabbri)",
            DataIncasso = appSilvia1.DataOra.Date,
        });
        var pagamentoDavide2 = await db.Pagamenti.AddAsync(new Pagamento
        {
            PazienteId = davide.Id, Importo = 65m, Metodo = MetodoPagamento.InStudio,
            Stato = StatoPagamento.Pagato, AppuntamentoId = appDavide2.Id,
            Origine = $"Seduta singola — terapia manuale 60 min (appuntamento {appDavide2.DataOra:dd/MM/yyyy}, {appDavide2.DataOra:HH:mm}, Dott. Andrea Conti)",
            DataIncasso = appDavide2.DataOra.Date,
        });
        await db.SaveChangesAsync();

        // ---------- Ricevute (numerazione progressiva annuale unica) ----------
        // Rinumerate in ordine cronologico di incasso: dalle due precedenti in poi, ogni
        // volta che si aggiunge uno storico più vecchio bisogna rifare la sequenza, non solo
        // inserirne un'altra — è la stessa numerazione che il commercialista vedrebbe.
        db.Ricevute.AddRange(
            new Ricevuta
            {
                NumeroProgressivo = 42, Anno = pagamentoMarco.DataIncasso!.Value.Year,
                PagamentoId = pagamentoMarco.Id, Importo = 380m,
                ImpostaBolloApplicata = true, InviataSistemaTs = false,
            },
            new Ricevuta
            {
                NumeroProgressivo = 41, Anno = pagamentoGiulia.DataIncasso!.Value.Year,
                PagamentoId = pagamentoGiulia.Id, Importo = 36m,
                ImpostaBolloApplicata = false, InviataSistemaTs = true,
            },
            new Ricevuta
            {
                NumeroProgressivo = 40, Anno = pagamentoAnnaValutazione.Entity.DataIncasso!.Value.Year,
                PagamentoId = pagamentoAnnaValutazione.Entity.Id, Importo = 70m,
                ImpostaBolloApplicata = false, InviataSistemaTs = true, OpposizioneSistemaTs = true,
            },
            new Ricevuta
            {
                NumeroProgressivo = 35, Anno = pagamentoDavide2.Entity.DataIncasso!.Value.Year,
                PagamentoId = pagamentoDavide2.Entity.Id, Importo = 65m,
                ImpostaBolloApplicata = false, InviataSistemaTs = true,
            },
            new Ricevuta
            {
                NumeroProgressivo = 34, Anno = pagamentoSilvia1.Entity.DataIncasso!.Value.Year,
                PagamentoId = pagamentoSilvia1.Entity.Id, Importo = 60m,
                ImpostaBolloApplicata = false, InviataSistemaTs = true,
            },
            new Ricevuta
            {
                NumeroProgressivo = 33, Anno = pagamentoChiara2.Entity.DataIncasso!.Value.Year,
                PagamentoId = pagamentoChiara2.Entity.Id, Importo = 60m,
                ImpostaBolloApplicata = false, InviataSistemaTs = true,
            },
            new Ricevuta
            {
                NumeroProgressivo = 32, Anno = pagamentoMatteo1.Entity.DataIncasso!.Value.Year,
                PagamentoId = pagamentoMatteo1.Entity.Id, Importo = 55m,
                ImpostaBolloApplicata = false, InviataSistemaTs = true,
            },
            new Ricevuta
            {
                NumeroProgressivo = 31, Anno = pagamentoDavide1.Entity.DataIncasso!.Value.Year,
                PagamentoId = pagamentoDavide1.Entity.Id, Importo = 60m,
                ImpostaBolloApplicata = false, InviataSistemaTs = true,
            },
            new Ricevuta
            {
                NumeroProgressivo = 30, Anno = pagamentoChiara1.Entity.DataIncasso!.Value.Year,
                PagamentoId = pagamentoChiara1.Entity.Id, Importo = 60m,
                ImpostaBolloApplicata = false, InviataSistemaTs = true,
            },
            new Ricevuta
            {
                NumeroProgressivo = 29, Anno = pagamentoAnnaSedutaManuale.Entity.DataIncasso!.Value.Year,
                PagamentoId = pagamentoAnnaSedutaManuale.Entity.Id, Importo = 60m,
                ImpostaBolloApplicata = false, InviataSistemaTs = true,
            },
            new Ricevuta
            {
                NumeroProgressivo = 28, Anno = pagamentoRobertoPrimaSeduta.Entity.DataIncasso!.Value.Year,
                PagamentoId = pagamentoRobertoPrimaSeduta.Entity.Id, Importo = 60m,
                ImpostaBolloApplicata = false, InviataSistemaTs = true,
            },
            new Ricevuta
            {
                NumeroProgressivo = 27, Anno = pagamentoPaoloTicketPrecedente.Entity.DataIncasso!.Value.Year,
                PagamentoId = pagamentoPaoloTicketPrecedente.Entity.Id, Importo = 36m,
                ImpostaBolloApplicata = false, InviataSistemaTs = true,
            },
            new Ricevuta
            {
                NumeroProgressivo = 26, Anno = pagamentoAnnaTecarPrecedente.Entity.DataIncasso!.Value.Year,
                PagamentoId = pagamentoAnnaTecarPrecedente.Entity.Id, Importo = 90m,
                ImpostaBolloApplicata = true, InviataSistemaTs = true,
            },
            new Ricevuta
            {
                NumeroProgressivo = 25, Anno = pagamentoMarcoSedutaPrimaDelPacchetto.Entity.DataIncasso!.Value.Year,
                PagamentoId = pagamentoMarcoSedutaPrimaDelPacchetto.Entity.Id, Importo = 60m,
                ImpostaBolloApplicata = false, InviataSistemaTs = true,
            }
        );

        // ---------- Impostazioni (singleton) ----------
        db.ImpostazioniAgenda.Add(new ImpostazioniAgenda());
        db.ImpostazioniListino.Add(new ImpostazioniListino());

        await db.SaveChangesAsync();
    }

    private static async Task<ApplicationUser> CreaUtente(
        UserManager<ApplicationUser> userManager, string email, string nome, string cognome, Ruolo ruolo)
    {
        var utente = new ApplicationUser
        {
            UserName = email, Email = email, EmailConfirmed = true,
            Nome = nome, Cognome = cognome, Ruolo = ruolo,
        };
        var result = await userManager.CreateAsync(utente, PasswordDemo);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Seed fallito per {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
        await userManager.AddToRoleAsync(utente, ruolo.ToString());
        return utente;
    }
}
