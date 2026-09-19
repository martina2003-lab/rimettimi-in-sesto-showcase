# Rimettimi in sesto

Portale di prenotazione per uno studio di fisioterapia privato, **accreditato per erogare anche prestazioni convenzionate SSN** — pensato per sostituire il flusso agenda cartacea + WhatsApp con cui molti piccoli studi lavorano ancora oggi.

**Demo online**: [rimettimi-in-sesto-hgd4hebmeadbdkgt.germanywestcentral-01.azurewebsites.net](https://rimettimi-in-sesto-hgd4hebmeadbdkgt.germanywestcentral-01.azurewebsites.net) — si entra scegliendo uno dei quattro ruoli, nessuna credenziale da digitare.

## Il problema

Lo studio preso a riferimento gestiva gli appuntamenti tra agenda cartacea e messaggi WhatsApp: ogni spostamento richiedeva tempo e non c'era modo di sapere a colpo d'occhio chi avesse in carico quale paziente.

## L'idea

Il paziente prenota online scegliendo direttamente il proprio fisioterapista — la **continuità terapeutica** conta nel percorso riabilitativo, ma resta una preferenza suggerita (chi ha già fatto sedute vede in cima il proprio terapista abituale), mai un vincolo. Fisioterapisti e coordinatrice lavorano sulla stessa agenda condivisa, ciascuno con la vista corrispondente al proprio ruolo; il titolare ha una vista di business separata, senza agenda propria.

Uno studio così è privato ma **accreditato per erogare prestazioni convenzionate SSN**: il portale gestisce quindi due percorsi paralleli, quello privato (pacchetti di sedute a pagamento) e quello convenzionato (cicli aperti da una ricetta del medico di base, con ticket e vincoli normativi propri).

## Stato del progetto

**Applicazione completa, funzionante per tutti e quattro i ruoli e pubblicata online**: si accede, si prenota, la segreteria conferma, il fisioterapista chiude la seduta, il pacchetto cala e la cassa emette ricevuta.

La demo è deliberatamente **senza marchio** e senza modulo email/password: è la correzione di una segnalazione "sito pericoloso" di Google Safe Browsing, dovuta a un modulo di login con un marchio sanitario reale su un indirizzo gratuito e condiviso (`*.azurewebsites.net`) — il profilo esatto che i classificatori antifrode leggono come pagina clone. Il racconto completo, verifiche di sicurezza comprese, è in [DEVLOG.md](DEVLOG.md).

La prima implementazione è deliberatamente un **portale dimostrativo**, non un sistema di produzione: gira su tier Azure economici, non invia email o SMS reali e simula i pagamenti. Le regole e il perché sono in [ARCHITETTURA.md](ARCHITETTURA.md) → "Note per il deploy demo".

- [requisiti.md](requisiti.md) — requisiti funzionali per ruolo e requisiti non funzionali (privacy/GDPR, sicurezza, performance)
- [ARCHITETTURA.md](ARCHITETTURA.md) — stack tecnico, modello dati, sicurezza, regole della versione demo
- [DEVLOG.md](DEVLOG.md) — decisioni di progetto, bug reali trovati e come sono stati diagnosticati

Questo repository è una versione curata per essere mostrata all'esterno: non contiene i mockup HTML né i dati clinici e anagrafici di riferimento, che sono rimasti in un repository privato insieme al nome e ai materiali reali dello studio da cui il progetto è partito.

## Ruoli

- **Paziente** — prenota, sceglie il fisioterapista, carica la propria ricetta SSN, vede storico e riepilogo della cartella clinica.
- **Fisioterapista** — gestisce la propria agenda, la propria bio professionale e la cartella clinica dei pazienti che tratta.
- **Coordinatrice / segreteria** — conferma ogni prenotazione, prenota anche per telefono/sportello, valida le ricette SSN, gestisce assenze e riassegnazioni, configura le regole operative dell'agenda.
- **Admin (titolare/soci)** — vista di business, senza agenda né accesso clinico: entrate per fonte, statistiche di lavoro dello staff, gestione utenti e listini.

## Funzionalità principali (MVP)

- Prenotazione, modifica e cancellazione appuntamenti online, con scelta diretta del fisioterapista.
- Conferma manuale di ogni prenotazione da parte della segreteria (nessuna auto-conferma); la richiesta blocca lo slot fino alla decisione.
- Prenotazione telefonica/allo sportello gestita dalla segreteria, anche per pazienti non registrati online (scheda provvisoria creata al volo per bloccare lo slot).
- Doppio percorso: pacchetti privati e cicli convenzionati SSN da ricetta.
- Agenda condivisa con vista differenziata per ruolo, e gestione di assenze, sostituzioni e chiusure.
- Cartella clinica digitale (anamnesi, valutazioni, piani di trattamento, note seduta), con accesso derivato dagli appuntamenti e tracciato in audit log.
- Conferme e cancellazioni notificate via email/SMS (nella versione demo sono simulate: registrate a sistema, mai spedite), avviso di disponibilità anticipata incluso, tramite la lista d'attesa della segreteria.

## Stack tecnico (sintesi)

ASP.NET Core Web API (.NET 8) + React/TypeScript, SQLite (anche in produzione, per scelta: vedi ARCHITETTURA.md), hosting su un unico App Service di Microsoft Azure che serve sia l'API sia l'interfaccia. Dettagli e motivazioni in [ARCHITETTURA.md](ARCHITETTURA.md).

## Sviluppo

.NET SDK 8 + Node.js, due terminali (backend su `:5131`, frontend su `:5173` — quest'ultimo l'indirizzo da aprire):

```bash
cd backend && dotnet run
cd frontend && npm install && npm run dev
```

Il database SQLite si crea da solo al primo avvio, con migration e dati demo già dentro. Nessuna credenziale: si entra scegliendo uno dei quattro ruoli.

Build di produzione: `cd backend && dotnet publish -c Release` (compila anche il frontend e lo include nell'artefatto — un unico servizio serve sia API che interfaccia).

## Limiti noti

Due regole di dominio restano provvisorie, in attesa di conferma da uno studio reale: cosa succede a una seduta quando il paziente non si presenta, e l'importo esatto della quota ticket regionale (dettagli e ragioni in [DEVLOG.md](DEVLOG.md)). Un dominio personalizzato risolverebbe la dipendenza dalla reputazione condivisa di `*.azurewebsites.net`, ma richiede un piano Azure a pagamento.
