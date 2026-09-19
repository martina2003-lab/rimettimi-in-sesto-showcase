# Requisiti — Rimettimi in sesto

Documento di riferimento per requisiti funzionali e non funzionali, per **uno studio di fisioterapia privato accreditato per prestazioni convenzionate SSN**. Per l'architettura tecnica vedi [ARCHITETTURA.md](ARCHITETTURA.md).

## Attori / ruoli

- **Paziente** — riceve le cure, prenota le proprie sedute (percorso privato o convenzionato SSN).
- **Fisioterapista** — eroga le sedute, tiene la cartella clinica dei propri pazienti.
- **Coordinatrice / segreteria** — gestisce l'operatività quotidiana: agenda, conferma delle prenotazioni, validazione delle ricette SSN, parametri operativi di scheduling. Non ha alcun accesso alla cartella clinica.
- **Admin (titolare/soci)** — non è la coordinatrice: è chi possiede/gestisce la struttura. Vista di business (entrate/uscite, performance dello staff), gestione utenti, listini e parametri di pricing.

## Requisiti funzionali per ruolo

### Paziente

**Registrazione e identità**
- Registrazione con email + password.
- Codice fiscale obbligatorio (in registrazione o alla prima prenotazione) — serve sia per identificare univocamente il paziente sia per far combaciare i dati con un'eventuale ricetta SSN.
- Nessun login tramite SPID/CIE nell'MVP (adatto a portali pubblici regionali, non a una struttura privata accreditata come questa).
- **Un account può gestire più pazienti collegati** (sé stesso, un figlio minore, un genitore anziano): prenota per ciascuno di loro, riceve le relative notifiche e presta i consensi dove ne ha titolo. Uno studio così tratta già oggi pazienti minori o sotto tutela — il modulo di consenso cartaceo prevede espressamente la firma di chi esercita la potestà genitoriale o la tutela.

**Scelta del fisioterapista**
- Il paziente sceglie sempre direttamente il fisioterapista, **anche alla primissima prenotazione** — non esiste uno smistamento/triage automatico gestito dallo studio. Chi non ha preferenze può selezionare "nessuna preferenza / prima disponibilità".
- Per un paziente già seguito, il terapista con cui ha fatto più sedute compare in cima alla lista con un badge tipo "Prenota di nuovo" / "Il tuo fisioterapista abituale" — **non è un legame fisso**: è calcolato dallo storico prenotazioni, e il paziente resta sempre libero di scegliere un altro terapista.
- Durante la scelta il paziente può consultare la **bio** di ciascun fisioterapista (informazioni personali e professionali, gestite dal fisioterapista stesso).
- La prima valutazione di un nuovo paziente è condotta direttamente dal fisioterapista scelto (accesso diretto, si veda la nota normativa in Glossario) — non è previsto un ruolo "fisiatra" separato nel sistema. **Punto aperto**: va confermato se lo studio di riferimento ha o no un fisiatra tra il personale interno.

**Prenotazione**
- **Durata della seduta**, in fasce da 30 minuti (30 min / 1 h / 1 h 30):
  - *Privato*: il paziente la sceglie liberamente prima di vedere gli orari disponibili.
  - *Convenzionato SSN*: non è una scelta del paziente — è **30 minuti per ogni distretto corporeo** che la ricetta prescrive di trattare in quella seduta (colonna cervicale, dorsale, lombare, arto superiore o inferiore, destro o sinistro), secondo il Nomenclatore SSN. Una ricetta con due distretti da trattare insieme genera una seduta di 60 minuti. Il sistema legge il numero di distretti dalla ricetta, non lo chiede al paziente.
- Prenotabile entro una finestra massima configurabile dalla Coordinatrice (valore di partenza suggerito: 60 giorni).
- Se il primo slot libero con il terapista scelto non è nella data desiderata, il paziente prenota comunque quello slot (non resta mai senza un appuntamento confermato) e può attivare un avviso automatico (email/SMS) per essere spostato prima se si libera qualcosa — sceglie se accettare o mantenere l'appuntamento originale.
- Ogni richiesta di appuntamento nasce con stato "richiesta" e diventa "confermata" solo dopo revisione manuale della Coordinatrice — **sempre**, sia per il percorso privato sia per quello convenzionato SSN. Nessuna auto-conferma.
- **La richiesta blocca lo slot** dal momento dell'invio: nessun altro paziente può richiedere quello stesso orario mentre è in attesa di conferma. Senza questo blocco la conferma manuale diventerebbe una lotteria tra pazienti che hanno richiesto lo stesso slot.
- **Il flusso di prenotazione è organizzato a step**, non su un'unica schermata: (1) percorso e durata/ricetta, (2) scelta del fisioterapista, (3) data e ora, (4) riepilogo e conferma prima dell'invio. Scelta basata su ricerca (i form multi-step convertono meglio oltre ~5 decisioni indipendenti, e il nostro flusso ne ha 6 non indipendenti tra loro) e sul comportamento di Doctolib, che usa lo stesso pattern. Il riepilogo finale (step 4) è esplicitamente nuovo: mostra tutte le scelte fatte e ricorda che la richiesta blocca lo slot, prima che il paziente la invii.
- Se la Coordinatrice non può confermare lo slot richiesto, può proporre **fino a 2-3 orari alternativi**: il paziente li riceve via email/SMS e li vede nel portale, e ne sceglie uno (oppure rifiuta). Gli slot proposti restano bloccati per tutta la validità della proposta. Se il paziente non risponde entro la scadenza (configurabile, valore di partenza suggerito: 48 ore), gli slot vengono liberati e la richiesta torna in carico alla Coordinatrice.
- Modifica/cancellazione consentita entro un preavviso minimo configurabile dalla Coordinatrice (valore di partenza suggerito: 24 ore); sotto quella soglia il paziente deve contattare direttamente lo studio.

**Percorso privato vs convenzionato SSN** (due categorie distinte, con regole proprie — si veda anche ARCHITETTURA.md)
- *Privato*: acquisto di un pacchetto di sedute a prezzo/durata decisi dallo studio (parametri configurabili dall'Admin, valore di partenza suggerito: 12 mesi di validità); pagamento online o in studio.
- *Convenzionato SSN*: il paziente carica online i dati/foto della propria ricetta/impegnativa (numero o NRE, medico prescrittore, codici). Numero di sedute, ticket dovuto e scadenza sono decisi dalla ricetta/regione, non dallo studio. La validazione reale della ricetta (autenticità, esenzioni) resta un controllo manuale della Coordinatrice — nessuna verifica automatica integrata nell'MVP.
- Un appuntamento SSN può essere **confermato dalla Coordinatrice prima che il ciclo sia aperto** (si veda "Validazione delle ricette SSN" più sotto) — lo slot è suo, ma sedute, durata e ticket restano indeterminati finché la ricetta non è validata. Il paziente deve vederlo: la propria vista appuntamenti riporta esplicitamente "ciclo non ancora aperto — la segreteria sta validando la tua ricetta" finché dura questa fase, non solo un generico "Confermato" che lascerebbe intendere che non manca più nulla.

**Cartella clinica**
- Visualizzazione del proprio storico appuntamenti.
- Di default un **riepilogo**: la propria diagnosi e il resoconto di quanto svolto in ciascuna seduta — non le note tecniche interne del fisioterapista. La scheda clinica completa resta in carico al fisioterapista; il paziente ne prende comunque visione al momento della firma.
- Azione esplicita "Richiedi copia completa della cartella clinica", che soddisfa il diritto di accesso GDPR (art. 15) anche alle note integrali.

**Consensi**
- Due consensi **distinti e prestati separatamente**, come nel modulo cartaceo oggi in uso: trattamento dei dati personali identificativi e trattamento dei dati sensibili. Non vanno collassati in un'unica casella "accetto": la granularità per le categorie particolari è un requisito del GDPR.
- Consenso informato al trattamento sanitario (rischi, controindicazioni): atto distinto dal consenso privacy, con contenuto proprio, ripreso da un modulo cartaceo reale rimasto nel repository privato.
- Ogni consenso è tracciato con data e **versione dell'informativa** accettata, così da poter sempre dimostrare a cosa il paziente avesse aderito.
- **Revoca**: sulla carta avviene per comunicazione scritta alla direzione sanitaria. Nel portale la revoca deve essere **facile quanto la concessione** (obbligo GDPR): se il consenso si presta con una spunta, non si può pretendere una lettera per ritirarlo. La comunicazione alla direzione sanitaria viene semmai generata dal sistema.

**Altro**
- Ricezione di conferme e promemoria via email/SMS.

### Fisioterapista

**Rapporto di lavoro**
- Ogni fisioterapista ha un tipo di contratto: **dipendente** (stipendio fisso, monte ore settimanale contrattuale) oppure **collaboratore a partita IVA** (pagato a prestazione). Dato **interno**, visibile solo a Coordinatrice e Admin — mai al paziente, che sceglie sempre liberamente il fisioterapista indipendentemente da questo.

**Disponibilità/orari** (differenziata per tipo di contratto)
- *Dipendente*: il pattern base di turni (giorni/mattina-pomeriggio) è impostato dall'Admin in base al contratto di lavoro; il fisioterapista può segnalare richieste di permesso/cambio turno, che la Coordinatrice approva.
- *Collaboratore*: gestisce autonomamente la propria disponibilità nel sistema.
- Per le richieste di prenotazione senza preferenza di terapista, il sistema suggerisce di default i dipendenti liberi prima dei collaboratori — solo un ordine di suggerimento, non un vincolo: la Coordinatrice vede sempre il tipo di contratto e decide lei.

**Profilo professionale (bio)**
- Ogni fisioterapista gestisce autonomamente, da una sezione dedicata del proprio portale, la propria bio: informazioni personali e soprattutto professionali (formazione, esperienza, aree di interesse).
- Questa bio è visibile al paziente al momento della scelta del fisioterapista.
- **Nota**: in questo tipo di studio i fisioterapisti eseguono più o meno tutte le prestazioni, quindi non serve una tassonomia strutturata di specializzazioni da usare come filtro di ricerca o come vincolo su chi può erogare cosa. Le competenze sono raccontate nella bio in forma libera, non modellate come dato strutturato.

**Agenda e pazienti**
- Vista sulla propria agenda (solo i propri appuntamenti).
- Accesso in lettura/scrittura alla cartella clinica (anamnesi, valutazioni funzionali, piani di trattamento, note per singola seduta) secondo questa regola: **un fisioterapista accede alla cartella clinica di un paziente se ha, o ha avuto, un appuntamento assegnato con lui**. La regola copre in modo naturale sia i pazienti abituali sia le sostituzioni per assenza di un collega, senza bisogno di permessi temporanei da concedere e revocare. Ogni accesso è tracciato nell'audit log; il paziente non riceve una notifica per ogni accesso (è già informato del cambio di terapista dalla riassegnazione dell'appuntamento).
- Marcatura delle sedute come completate o no-show.
- Esegue lui stesso la prima valutazione funzionale di un nuovo paziente (accesso diretto, si veda Glossario) — nessun ruolo "fisiatra" separato previsto nel sistema (punto comunque aperto, si veda sotto).

**Assenze**

Quattro casi distinti, con impatti molto diversi:

1. **Chiusura dello studio** (festività, chiusura estiva) — non è un'assenza individuale: blocca tutti i fisioterapisti. Gestita centralmente (calendario annuale impostato dall'Admin; chiusure impreviste inserite dalla Coordinatrice).
2. **Assenza pianificata con preavviso** (ferie, permesso, formazione) — inserita prima che gli slot vengano prenotati: il sistema semplicemente non rende prenotabili quelle fasce, nessun impatto sui pazienti.
   - *Dipendente*: richiede l'assenza, la Coordinatrice approva; l'assenza incide sul monte ore contrattuale.
   - *Collaboratore*: modifica direttamente la propria disponibilità, senza approvazione.
3. **Assenza improvvisa** (malattia, imprevisto) su appuntamenti già confermati — il sistema segnala gli appuntamenti coinvolti e suggerisce possibili sostituti disponibili, ma è sempre la **Coordinatrice** a decidere come gestirli (riassegnazione a un collega, oppure cancellazione con recupero da concordare). Nessuna riassegnazione automatica.
4. **Assenza di lunga durata** (maternità, infortunio, aspettativa) — impatto strutturale:
   - Il fisioterapista non è selezionabile dai pazienti per tutto il periodo; chi lo aveva come "abituale" vede un avviso esplicito ("non disponibile in questo periodo") con le alternative, invece di un badge che porta a un vicolo cieco.
   - La Coordinatrice riceve una **lista di lavoro dei pazienti impattati, ordinata per urgenza**: prima quelli con un ciclo SSN in corso e finestra di completamento in scadenza (rischio concreto di perdere le sedute e dover richiedere una nuova impegnativa al medico di base), poi i pacchetti privati in corso, poi il resto.

**Priorità di riassegnazione** — in tutti i casi con appuntamenti già presi, l'ordine di urgenza è: cicli SSN con finestra di completamento in scadenza → pacchetti privati in corso → prenotazioni singole.

**Struttura della cartella clinica**

Derivata dai moduli cartacei reali usati dallo studio preso a riferimento (trascrizione rimasta nel repository privato). La scheda è organizzata **per ciclo di trattamento, non per singola seduta**, con apertura e chiusura formali:

- Anagrafica del paziente (già presente nella scheda paziente: non va richiesta di nuovo).
- Provenienza del paziente.
- Anamnesi patologica remota, esame obiettivo, esami specialistici eseguiti e consigliati, diagnosi della patologia in corso, programma riabilitativo, indicazioni per il paziente — tutti a **testo libero**, come oggi: la versione digitale non deve imporre tassonomie che i fisioterapisti non usano.
- **Note del fisioterapista ed eventuali indicazioni in caso di sostituzione**: campo già presente sulla carta, conferma che il passaggio di consegne tra colleghi è prassi corrente.
- **VAS (scala analogica visiva) a inizio e a fine ciclo**, valore numerico 0-10: è l'unico dato strutturato e misurabile del modulo, pensato per il confronto prima/dopo.
- **Controindicazioni dichiarate dal paziente** (dodici voci sì/no: pacemaker, gravidanza, neoplasia in atto o pregressa, epilessia, lesioni cutanee o fratture, stato infiammatorio acuto, disturbi cardiocircolatori, mezzi di sintesi o protesi, grave osteoporosi, tendenza ad emorragie, protesi acustiche, allergia ai FANS), più interventi chirurgici subiti e terapie farmacologiche in atto. **Scelta deliberata**: restano un dato consultabile in scheda, **senza alcun avviso automatico** quando si prenota una terapia strumentale a un paziente che ne ha dichiarata una — come funziona oggi sulla carta. Decisione presa consapevolmente, da non reintrodurre in fase di sviluppo senza una scelta esplicita.
- Apertura e chiusura del ciclo: data di inizio terapia e data di fine terapia, con firma del fisioterapista e del medico responsabile.
- **Riepilogo di seduta**: al termine di ogni appuntamento il fisioterapista registra un breve resoconto di quanto svolto. È l'unico elemento per singola seduta della cartella (tutto il resto è per ciclo) ed è **una funzionalità nuova rispetto alla carta**, dove non esiste alcun campo equivalente. Alimenta il riepilogo che il paziente vede nel proprio portale.

**Figure mediche**: nei moduli compaiono un "Medico responsabile" che firma inizio e fine terapia, un "Medico Fisiatra/Fisioterapista" che firma il consenso informato e una "direzione sanitaria" destinataria delle revoche. Queste figure **non sono un ruolo del portale**: compaiono come firmatari sui documenti generati, senza account né accesso al sistema.

**Chiuso il 12 settembre 2026**: nessuna riassegnazione stabile d'ufficio. Il "fisioterapista abituale" resta un valore calcolato dallo storico, mai un legame fisso da riassegnare — coerente col principio guida del progetto. Per gli appuntamenti futuri non ancora presi, il paziente sceglie semplicemente un altro fisio col normale flusso di prenotazione, come per qualunque prenotazione (privato o SSN): non serve un fisio di fiducia per forza, e non è un problema se cambia. La lista di lavoro della Coordinatrice (sopra) resta solo per gli appuntamenti **già presi** che cadono nel periodo di assenza — quelli vanno comunque gestiti uno per uno perché lo slot esiste già in agenda.

### Coordinatrice / segreteria

La sua vista del portale è organizzata in **tre sezioni**, ciascuna per un'area di competenza distinta — non un'unica lista mescolata, per evitare che compiti molto diversi si confondano:

1. **Pazienti** — anagrafica, prenotazioni, calendario, ricette SSN.
2. **Fisioterapisti** — disponibilità, assenze, tipo di contratto.
3. **Cassa** — pagamenti, ticket, ricevute.

I parametri configurabili dell'agenda (finestra di prenotazione, preavviso di cancellazione, soglia lista d'attesa) non sono una sezione a sé: vivono in un pannello impostazioni leggero, usato raramente.

#### Sezione Pazienti

**Scheda paziente** (ricerca per nome/codice fiscale/telefono), strutturata in:
- **Anagrafica**: dati base + stato (scheda provvisoria / consenso raccolto).
- **Percorso attivo**: privato (pacchetto, sedute residue) o SSN (ricetta, ticket, sedute residue, finestra di completamento).
- **Storico appuntamenti**: passati e futuri, con quale fisioterapista.
- **Documenti**: ricette caricate/allegate.
- **Note operative** (non cliniche — es. "spesso in ritardo", "preferisce il mattino"): esplicitamente separate dalla cartella clinica, che resta riservata al fisioterapista secondo la regola di accesso già definita. Servono a darle uno strumento di lavoro senza violare la minimizzazione dei dati clinici.

**Calendario a vista d'occhio**: griglia con tutti gli appuntamenti dello studio, filtrabile per fisioterapista, con colore diverso per stato (richiesto/confermato) e percorso (privato/SSN).

**Coda delle richieste online**: conferma manuale di ogni richiesta (privata o convenzionata) prima che diventi definitiva. Ogni richiesta in attesa tiene bloccato il proprio slot, quindi le richieste ferme da troppo tempo vengono segnalate in coda per evitare che blocchino l'agenda inutilmente.

**Proposta di slot alternativo** — percorso di eccezione, quando lo slot richiesto non è confermabile (terapista resosi indisponibile, ricetta che impone di anticipare, esigenze organizzative):
- La Coordinatrice propone **fino a 2-3 orari alternativi**; ognuno resta bloccato per la durata della proposta.
- Il paziente riceve la proposta via email/SMS e la vede nel portale: ne sceglie uno o rifiuta.
- Se non risponde entro la scadenza (parametro configurabile, valore di partenza suggerito: 48 ore), gli slot vengono liberati e la richiesta torna in coda segnalata come "proposta scaduta" — la Coordinatrice decide se richiamare il paziente o chiudere.
- In alternativa, come già previsto per le cancellazioni, la Coordinatrice può semplicemente **telefonare e concordare a voce**, spostando l'appuntamento direttamente senza inviare nessuna proposta formale.

**Validazione delle ricette SSN**

Le ricette in attesa vivono in una **coda dedicata**, non solo dentro la singola scheda paziente: senza una vista d'insieme la coordinatrice non sa quali cicli sono fermi. La coda mostra per ciascuna l'esito sintetico dei controlli automatici.

La schermata di validazione affianca il **documento caricato** (a sinistra) ai **dati da confermare** (a destra): la validazione è un confronto tra ciò che si legge sulla foto e ciò che entra a sistema, quindi le due cose devono stare sott'occhio insieme.

*Controlli automatici* — coerenza formale, verificabile dal sistema senza integrazioni esterne:

| Controllo | Esito se fallisce |
|---|---|
| Codice fiscale sulla ricetta = codice fiscale della scheda | **Bloccante** |
| NRE nel formato previsto (15 caratteri alfanumerici) | Avviso |
| NRE non già usato in un ciclo aperto (una ricetta non è spendibile due volte) | **Bloccante** |
| Ricetta entro la propria validità | **Bloccante** |
| Sedute prescritte entro il tetto regionale (10, valore preso a riferimento) | Avviso, con proposta di aprire il ciclo per 10 |
| Branca 93 — medicina fisica e riabilitativa | **Bloccante** |

Un controllo bloccante **disabilita** la validazione: restano possibili solo integrazione o rifiuto. Un avviso segnala e basta — la coordinatrice decide.

*Verifica a vista* — quel che il sistema non può sapere: autenticità del documento, leggibilità, correttezza dell'esenzione. Sono i campi che la coordinatrice conferma o corregge: distretti da trattare (determinano la durata della seduta, 30 minuti ciascuno), numero di sedute da autorizzare, codice di esenzione (o ticket dovuto da quota regionale), tipo di patologia da cui discende la finestra di completamento (30 giorni ortopedica/traumatica, 60 neurologica acuta, oppure data indicata a mano).

Prima di confermare, un riquadro riassume in linguaggio esplicito **cosa produce la validazione**: quante sedute apre, di che durata, entro quando vanno completate, quale voce compare in Cassa. È il punto in cui la ricetta smette di essere un documento e diventa un ciclo con effetti economici e di agenda.

*I tre esiti possibili* — la parte che mancava: "respinta" non è una risposta sufficiente, perché lascia aperta la domanda vera, cosa succede agli appuntamenti già fissati.

1. **Validata** → si apre il ciclo, si fissano durata seduta e finestra di completamento, si genera la voce ticket in Cassa (o si registra l'esenzione a € 0).
2. **Integrazione richiesta** (foto illeggibile, manca il retro, dati non leggibili) → la ricetta resta in sospeso e il ciclo non si apre, ma **gli appuntamenti già fissati non si toccano**: il paziente può ancora portare il documento in studio. Si sceglie il motivo e il canale con cui avvisarlo — inclusa l'opzione "l'ho già chiamato", nessuna notifica automatica.
3. **Respinta** (scaduta, già utilizzata, branca sbagliata, intestata ad altri, non autentica) → il ciclo convenzionato non si può aprire, e va deciso esplicitamente il destino degli appuntamenti in agenda: *mantenerli* in attesa di una nuova impegnativa, *convertire il percorso a privato* (tariffa piena, da concordare prima con il paziente — non è una scelta che si possa fare al posto suo), oppure *annullarli* liberando gli slot. Nessuna di queste è automatica.

Ogni esito diverso dalla validazione lascia traccia nelle **note operative** della scheda, così la decisione resta ricostruibile a distanza di settimane.

**Rapporto tra la coda Richieste e la coda Ricette SSN**

Sono due decisioni distinte sullo stesso paziente: *questo slot glielo do?* (agenda, urgente, il paziente aspetta e lo slot è bloccato) e *questo ciclo lo apro?* (amministrativa ed economica, può arrivare giorni dopo). Non vanno fuse, e **la validazione della ricetta non può essere un prerequisito per confermare l'appuntamento**: nel flusso telefonico la ricetta arriva tipicamente alla prima seduta, quindi pretenderla prima renderebbe impossibile prenotare al telefono un percorso SSN.

Vanno però rese consapevoli l'una dell'altra, altrimenti la stessa persona compare in due code che si ignorano e ogni decisione si prende al buio. Il collegamento è uno **stato derivato dalla ricetta del paziente**, calcolato al momento, mai memorizzato sull'appuntamento — stessa logica del "fisioterapista abituale":

- In **coda Richieste** un appuntamento SSN dichiara lo stato del ciclo che dovrebbe giustificarlo (*non ancora validata* / *in attesa di integrazione* / *N controlli bloccanti* / *respinta*), con un collegamento diretto alla validazione. La conferma resta possibile: quello che cambia è che non è più cieca.
- In **agenda** il chip porta una barra laterale colorata quando il ciclo non è aperto. Non sostituisce confermato/richiesto: ci si sovrappone, perché sono due assi diversi.
- In **coda Ricette SSN** ogni riga dice quanti appuntamenti sta tenendo fermi e da quante ore, e la coda è ordinata per **urgenza d'agenda**, non per anzianità del documento: si apre prima la ricetta che sta bloccando uno slot.
- Alla **conferma di uno slot SSN senza ciclo aperto**, l'avvertimento non è uno solo: dipende dal fatto che un documento esista già in scheda o no, perché sono due situazioni diverse e non vanno confuse.
  - **Documento assente o da rifare** (nessuna ricetta caricata — tipico della prenotazione telefonica —, oppure una ricetta già respinta o in attesa di integrazione): qui ha davvero senso chiedere, perché non sappiamo se/quando arriverà. Compare *"ricetta non ancora validata, procedere lo stesso?"*, superabile — prenotare online e portare il documento di persona è un percorso previsto, non un'anomalia — e che chiede **dove si trova la ricetta**, perché è quello che dice a chi tocca la mossa successiva:
    - *la porta in studio alla prima seduta* — si aspetta il paziente;
    - *ce l'abbiamo già qui in cartaceo* — consegnata allo sportello: va scansionata e inserita a mano, la palla è nostra, e infatti la conferma porta dritti alla schermata di inserimento;
    - *la invia o la carica dal portale* — si aspetta il paziente, con ricaduta sul cartaceo se non arriva.

    A questo si accompagna un **appunto libero per la segreteria** ("cartacea nel raccoglitore", "esenzione da verificare col medico"). La scelta finisce nelle note operative della scheda, nel tab Documenti, e viaggia insieme all'avviso ovunque compaia: in agenda il chip passa da *"ricetta da validare"* a *"ricetta in arrivo"* o *"ricetta da inserire"*. Nella coda Ricette diventa un ordinamento: a parità di urgenza d'agenda vengono prima le ricette il cui documento è già in studio, perché su quelle non stiamo aspettando nessuno. La validazione chiude l'attesa.
  - **Documento già presente, in coda di validazione** (caso tipico di chi ha caricato la foto online durante la prenotazione): qui non si chiede nulla al paziente — il documento c'è, non manca lui, manca solo che la segreteria lo apra. L'avvertimento diventa un pannello informativo — fonte e data del caricamento, eventuali controlli automatici bloccanti da risolvere — con due sole azioni: aprire subito la validazione, oppure confermare lo slot lasciando la ricetta in coda. Non chiedere "dov'è la ricetta" quando è già arrivata è la correzione a un buco reale: prima che venisse distinta, un paziente che aveva già caricato la foto online si ritrovava con l'avviso "la porta alla prima seduta" mentre la sua ricetta era visibilmente in coda nella schermata accanto — due affermazioni contraddittorie sulla stessa persona.
- Il **badge in sidebar** somma richieste e ricette: sono entrambe cose da lavorare. La sotto-navigazione le tiene separate.
- Nel **rifiuto della ricetta**, gli appuntamenti già confermati e quelli ancora richiesti vanno contati e trattati separatamente: su un appuntamento ancora in coda "mantieni" significa lasciarlo lì segnalato come senza ricetta valida, non semplicemente non toccarlo.

**Prenotazione per conto del paziente (telefono/sportello)** — distinzione fondamentale: **scheda anagrafica** (dati del paziente) e **account con accesso al portale** (login) sono due cose separate. Un paziente può avere una scheda senza essersi mai registrato online — è il caso normale per chi prenota sempre per telefono.
- Per un paziente con scheda già esistente: la Coordinatrice lo cerca e crea l'appuntamento direttamente dalla propria vista agenda (non dal wizard guidato del paziente). L'appuntamento nasce **già confermato**, saltando lo stato "richiesto" — è lei stessa il confermatore.
- Percorso SSN al telefono: la ricetta non può essere fotografata durante una chiamata. L'appuntamento viene fissato subito come "SSN da confermare"; la ricetta si valida dopo, quando il paziente la porta fisicamente (tipicamente al primo appuntamento) o la invia.
- Per un paziente mai registrato: la Coordinatrice crea una **scheda provvisoria** minima (nome, cognome, telefono obbligatori; codice fiscale ed email facoltativi) solo per bloccare lo slot. Il **consenso privacy/dati sanitari non si raccoglie per telefono** (non ha forma verificabile): si raccoglie di persona alla prima seduta, prima che il fisioterapista scriva qualsiasi dato clinico.
- Se quel paziente si registra poi da sé sul portale, il sistema cerca una scheda esistente per codice fiscale (o, in assenza, nome+cognome+telefono con conferma esplicita) e propone di collegarla, invece di creare un duplicato.

**Lista d'attesa**: gestione degli avvisi automatici di disponibilità anticipata.

**Nessun accesso clinico**: la coordinatrice vede i dati di scheduling e le note operative sopra descritte, **mai la cartella clinica** — né integralmente né in parte, nemmeno nei casi di sostituzione di un fisioterapista assente. È una decisione presa, non un'assunzione provvisoria, e discende dal principio di minimizzazione dei dati: per svolgere il proprio lavoro non ha bisogno del dato clinico.

#### Sezione Fisioterapisti

- Vista sulla disponibilità di tutti i fisioterapisti, con tipo di contratto (dipendente/collaboratore) in evidenza.
- Approvazione delle richieste di ferie/permesso/cambio turno dei fisioterapisti dipendenti.
- Inserimento di chiusure impreviste dello studio.
- Gestione delle assenze: decide caso per caso come coprire gli appuntamenti impattati (riassegnazione a un collega disponibile, dando priorità implicita ai dipendenti liberi ma potendo sempre scegliere liberamente, oppure cancellazione con recupero da concordare), lavorando su una lista ordinata per urgenza (cicli SSN in scadenza per primi).
- Cancellazione di un appuntamento per assenza del fisioterapista: sceglie caso per caso se ha già avvisato il paziente altrove (es. telefonicamente, nessuna notifica automatica) oppure se il sistema deve inviare una notifica email/SMS (sempre con approvazione esplicita prima dell'invio).

#### Sezione Cassa

Gestione economica quotidiana, organizzata in tre viste — la giornata in corso, l'arretrato, i documenti emessi — perché rispondono a tre domande diverse in tre momenti diversi.

**Principio di fondo: nessuna voce di cassa nasce dal nulla.** Ogni riga porta con sé la propria **origine** (il ciclo SSN e il suo NRE, il pacchetto acquistato, l'appuntamento erogato con data e terapista). Senza, una riga di cassa diventa inverificabile dopo poche settimane.

**Le voci e le loro regole** — non sono tutte uguali:
- **Ticket SSN**: dovuto **una volta per ciclo, non a seduta**. Nasce nel momento in cui la ricetta viene validata, non quando il paziente si presenta. Se c'è un'esenzione, la voce esiste comunque ma a € 0, con il codice indicato: serve a dimostrare *perché* non si è incassato nulla.
- **Pacchetto privato**: pagato all'acquisto, per l'intero pacchetto.
- **Seduta singola / terapia strumentale privata**: per prestazione.
- **Incasso vario**: voce registrata a mano, non collegata a un ciclo o a un pacchetto (recuperi, prestazioni fuori pacchetto).

**Vista Giornata** — totale incassato, totale ancora aperto, ricevute emesse, e la **ripartizione per metodo di pagamento** (contanti / POS / online), che è il dato che serve alla quadratura di fine giornata.

**Vista Da riscuotere** — le voci aperte, comprese quelle di giornate precedenti, ordinate dalla più vecchia e con l'**anzianità in evidenza**: una vista limitata a "oggi" nasconde proprio l'arretrato, che è la cosa che va vista. Da qui si incassa o si sollecita.

**Vista Ricevute** — i documenti emessi, con numerazione progressiva annuale. Elementi obbligatori di una ricevuta per prestazione sanitaria:
- ragione sociale, indirizzo e partita IVA della struttura;
- intestazione al paziente **con codice fiscale** — serve sia per la detrazione sia per la trasmissione al Sistema TS;
- descrizione della prestazione e importo;
- dicitura di **esenzione IVA ex art. 10 n. 18 DPR 633/72** (prestazioni sanitarie di diagnosi, cura e riabilitazione);
- **imposta di bollo di € 2,00** sulle operazioni esenti di importo superiore a € 77,47, esposta in ricevuta;
- indicazione dell'invio al **Sistema Tessera Sanitaria**, oppure dell'**opposizione del paziente** a tale invio.

**Opposizione al Sistema TS**: il paziente ha diritto di opporsi alla trasmissione della spesa sanitaria al 730 precompilato. È una scelta che va **registrata al momento dell'incasso**, non presa a voce e dimenticata: compare quindi come campo esplicito nella registrazione del pagamento e come dicitura sul documento.

**Storno, non cancellazione**: una ricevuta emessa non si elimina. Si emette un documento di storno di pari importo e segno opposto, che consuma il numero progressivo successivo e cita quello stornato. Vale il vincolo di conservazione decennale dei dati fiscali già previsto più sotto.

**Chiusura di cassa**: a fine giornata si confronta il **contante effettivamente in cassa** con quello registrato; uno scostamento va giustificato prima di chiudere. POS e incassi online si riconciliano con l'estratto del provider, non a mano.

#### Impostazioni (pannello leggero, non una sezione principale)

Configurazione dei **parametri operativi dell'agenda**: finestra massima di prenotazione anticipata, preavviso minimo di cancellazione/modifica, soglia minima per l'iscrizione all'avviso di disponibilità, validità di una proposta di slot alternativo.

### Admin (titolare/soci)

Non è la coordinatrice: rappresenta chi possiede/gestisce la struttura, con una vista di business, non operativa quotidiana. **Ruolo puramente gestionale**: il titolare non tratta pazienti, quindi non ha agenda propria né accesso clinico.

**Gestione e configurazione**
- Gestione degli account (creazione/disattivazione fisioterapisti e coordinatrice), incluso il tipo di contratto (dipendente/collaboratore) e, per i dipendenti, il pattern base di turni da contratto di lavoro.
- Configurazione dei **parametri di business/listino**: prezzi e durata dei pacchetti privati, numero sedute/ticket standard per i cicli convenzionati SSN, orari di apertura dello studio, calendario annuale delle chiusure (festività, chiusura estiva).

**Le due dimensioni incrociate delle entrate** (dal 18 settembre 2026 vive dentro la vista Andamento, non più come voce di menu a sé — vedi sotto)

Solo entrate — **nessuna gestione dei costi** (affitto, utenze, stipendi, ammortamento macchinari): quelli restano fuori dal portale, che gestisce prenotazioni e cure, non contabilità.

Le entrate si leggono su **due dimensioni incrociate**, non su tre categorie parallele:

| | Terapia manuale | Terapia strumentale |
|---|---|---|
| **Privato** | pagamento del paziente | pagamento del paziente |
| **Convenzionato SSN** | ticket | ticket |

Le terapie strumentali non sono una terza fonte a sé: sono prescritte dal medico e passano quindi dalla ricetta quando il percorso è convenzionato, mentre un paziente privato può pagarle direttamente. Incrociare le due dimensioni permette di vedere sia il mix privato/convenzionato sia quanto pesano le strumentali all'interno di ciascuno.

**Vista personale**

Elenco dei fisioterapisti diviso per tipo di contratto (dipendenti / partita IVA); selezionando una persona si aprono le sue statistiche di lavoro:
- **Ore di presenza/turno e ore di sedute effettivamente erogate, a confronto** — la differenza tra le due è il tasso di saturazione, l'indicatore più utile per capire se lo studio sta impiegando bene il personale (benchmark di settore per cliniche performanti: 75-85%; oltre il 90% è segnale di sovraccarico, non di efficienza).
- Numero di pazienti in cura e di sedute erogate nel periodo.
- *(Da rivedere in un round dedicato: quali altri indicatori includere.)*

**Vista andamento nel tempo** (rivista il 16 settembre e il 17-18 settembre 2026, dettaglio in [DEVLOG.md](DEVLOG.md))

Le altre due viste sono fotografie del presente: questa dà la profondità storica che manca. **Dal 18 settembre assorbe anche l'ex vista "Entrate"**, che come voce di menu separata non esiste più: rispondevano entrambe alla stessa domanda ("come vanno le entrate") con pezzi diversi, e tenerle separate significava due schermate che potevano raccontare storie leggermente diverse sullo stesso dato.
- **Tre granularità navigabili avanti e indietro** — settimana, mese, anno — non solo il mese: con poco storico reale, "questa settimana" e "quest'anno" raccontano storie diverse, non la stessa a scale diverse.
- Il confronto è **in euro con il periodo immediatamente precedente**, sempre. **Il confronto con lo stesso periodo dell'anno prima è tornato** (18 settembre 2026): il seed ora porta anche un po' di storico 2025 sugli stessi mesi già popolati nel 2026, apposta per renderlo possibile — quando quella base è a zero (es. i mesi senza storico 2025), la percentuale resta onestamente assente invece di mostrare una crescita "infinita" da zero.
- Risponde alla domanda che nessuna delle altre due copre: lo studio sta crescendo o calando — e ora anche cosa c'è **già impegnato in agenda** per i periodi futuri (appuntamenti confermati non ancora avvenuti), invece di mostrarli semplicemente vuoti.
- **L'incrocio manuale/strumentale × privato/SSN è tornato, ma solo a grana anno**: un motivo pratico lo teneva fuori (con poco storico l'incrocio a quattro celle era quasi sempre vuoto per metà) — ma nasconderlo del tutto voleva dire perdere l'unica lettura che l'ex vista Entrate offriva e questa pagina no. Il compromesso: la griglia compare solo quando si guarda l'anno intero, dove ha davvero senso come dato "da portare al commercialista", e sparisce a grana mese/settimana dove tornerebbe a essere per metà vuota.

**Nessun accesso clinico**, né ai dati nominativi dei pazienti oltre a quanto serve per la parte economica.

## Requisiti non funzionali

### Privacy / GDPR

I dati clinici sono **dati sanitari**, categoria particolare ai sensi dell'art. 9 GDPR. Questo comporta, fin dal design:

- Consenso esplicito del paziente, tracciato con data e versione dell'informativa.
- Controllo degli accessi per ruolo (RBAC) granulare, applicato lato backend — non solo nascosto in UI.
- Audit log di ogni accesso alla cartella clinica (chi, quando, quale paziente).
- Cifratura dei dati a riposo e in transito.
- Diritto di accesso da parte del paziente (si veda "Richiedi copia completa della cartella clinica" sopra).
- Accordi (DPA) con i fornitori terzi che trattano dati per conto dello studio: Azure, gateway di pagamento, provider email/SMS.

### Conservazione e cancellazione dei dati

La cartella clinica va conservata **illimitatamente**: la circolare del Ministero della Sanità n. 900 del 19/12/1986 la qualifica come atto ufficiale, e l'obbligo vale anche per le strutture private. La documentazione diagnostica allegata si conserva almeno 20 anni.

Questo obbligo **prevale sul diritto alla cancellazione**: l'art. 17(3) GDPR esclude il diritto all'oblio quando la conservazione è necessaria per adempiere a un obbligo legale o per motivi di interesse pubblico nel settore sanitario.

| Categoria di dati | Conservazione | Cancellabile su richiesta del paziente |
|---|---|---|
| Cartella clinica (anamnesi, valutazioni, VAS, riepiloghi di seduta) | Illimitata | No — obbligo di legge |
| Documenti diagnostici allegati (referti, immagini) | Almeno 20 anni | No |
| Dati fiscali (pagamenti, ticket, ricevute) | 10 anni (art. 2220 c.c.) | No |
| Anagrafica e contatti | Finché servono a mantenere riferibile la cartella clinica | Solo in parte |
| Account e credenziali di accesso | Fino alla chiusura richiesta dal paziente | Sì |
| Note operative della segreteria | Finché il paziente è attivo; poi cancellate | Sì |
| Audit log degli accessi clinici | 10 anni, allineati al termine ordinario di prescrizione | No |

**Conseguenza per il portale**: la cancellazione di un paziente non è mai totale, ma un'operazione selettiva — si eliminano account, contatti, note operative e preferenze, mentre la cartella clinica resta, necessariamente riferibile alla persona (una cartella anonima non assolverebbe l'obbligo di conservazione). Il portale non deve quindi offrire al paziente un comando "cancella i miei dati" che prometta più di quanto la legge consenta: va spiegato con chiarezza cosa viene cancellato e cosa no.

### Monitoraggio del lavoro (art. 4 Statuto dei Lavoratori)

La vista personale dell'Admin (ore lavorate, sedute erogate, saturazione) costituisce a tutti gli effetti un monitoraggio dell'attività dei lavoratori. Un gestionale con queste caratteristiche rientra nell'ambito dell'art. 4 dello Statuto dei Lavoratori, ma il nostro caso ricade nelle esenzioni introdotte dal Jobs Act (D.Lgs. 151/2015): sono esclusi dall'obbligo di accordo sindacale gli strumenti utilizzati dal lavoratore per rendere la prestazione lavorativa e quelli di registrazione degli accessi e delle presenze — esattamente ciò che il portale è per i fisioterapisti.

Resta però obbligatoria una **informativa adeguata ai lavoratori** su quali dati vengono raccolti e con quali finalità, con uso proporzionato dei dati stessi. Senza informativa, i dati raccolti rischiano di essere inutilizzabili (per esempio in un procedimento disciplinare).

### Sicurezza

- Autenticazione robusta; MFA opzionale per lo staff (fisioterapista, coordinatrice, admin).
- Log degli accessi.
- Backup regolari.
- Ambienti separati per sviluppo e produzione.

### Performance / disponibilità

- Carico atteso basso/medio: un solo studio, poche decine di utenti concorrenti al massimo.
- Nessun bisogno di scalabilità enterprise.
- Backup e disaster recovery di base sono sufficienti; non serve un SLA multi-9.

## Glossario

- **Fisioterapista di riferimento** — non un legame fisso: è il fisioterapista con cui il paziente ha fatto più sedute, calcolato dallo storico prenotazioni. Il paziente resta sempre libero di sceglierne un altro.
- **Accesso diretto** — possibilità per il fisioterapista, in uno studio privato, di prendere in carico un nuovo paziente e condurre la prima valutazione funzionale autonomamente, senza una visita medica/fisiatrica preliminare (L. 3/2018, D.M. 741/1994). Non riguarda diagnosi mediche, prescrizione di farmaci o richiesta di esami strumentali, di competenza esclusiva del medico.
- **Seduta** — singolo incontro tra paziente e fisioterapista, corrisponde a un appuntamento confermato.
- **Pacchetto (privato)** — insieme di N sedute a pagamento libero acquistabili insieme, con relativo scalare delle sedute residue a ogni appuntamento completato.
- **Ricetta / impegnativa** — prescrizione del medico di base (o specialista) che dà diritto a un ciclo di sedute in convenzione SSN, a fronte di un ticket. Può essere cartacea o dematerializzata (con codice NRE). Riporta un codice di prestazione (branca 93 = fisioterapia/riabilitazione), il quesito diagnostico ed eventuale esenzione ticket.
- **NRE (Numero Ricetta Elettronica)** — codice univoco a 15 caratteri che identifica una ricetta dematerializzata nel Sistema Tessera Sanitaria.
- **Distretto corporeo** — porzione anatomica su cui verte una prestazione SSN (colonna cervicale, dorsale, lombare, arto superiore/inferiore destro o sinistro). Ogni distretto prescritto nella ricetta corrisponde a 30 minuti di seduta secondo il Nomenclatore; più distretti trattati insieme allungano la seduta di 30 minuti ciascuno.
- **Ciclo convenzionato SSN** — percorso di sedute (tipicamente 10) aperto da una ricetta valida, con numero sedute/ticket/scadenza decisi dalla ricetta stessa, non dallo studio. Oltre alla scadenza della ricetta esiste una **finestra di completamento del ciclo** (indicativamente 30 giorni per patologie traumatiche ortopediche, 60 per patologie neurologiche in fase acuta): se il ciclo si interrompe oltre quella finestra, il paziente deve richiedere una nuova impegnativa al medico di base. Per la **Regione Lazio**, scelta come riferimento normativo di questo progetto, risulta confermato il tetto di **10 sedute per ciclo**, con il numero effettivo deciso dal medico nella prescrizione entro quel limite — il sistema deve quindi leggerlo dalla ricetta, mai fissarlo. I limiti sul numero di cicli annui per paziente restano da verificare con lo studio o con la ASL.
- **Cartella clinica** — anamnesi, valutazioni funzionali, piani di trattamento e note delle singole sedute, riferite a un paziente e mantenute dal/i fisioterapista/i che lo hanno in carico.
- **Fisioterapista dipendente** — rapporto di lavoro subordinato, stipendio fisso indipendente dal numero di pazienti seguiti, monte ore settimanale da contratto (CCNL Sanità Privata).
- **Fisioterapista collaboratore (partita IVA)** — libero professionista pagato a prestazione, gestisce autonomamente la propria disponibilità.
- **Scheda paziente** — dati anagrafici del paziente nel sistema. Esiste indipendentemente da un account: può essere creata dalla Coordinatrice (es. prenotazione telefonica) prima ancora che il paziente si registri online.
- **Scheda provvisoria** — scheda paziente minima (nome, cognome, telefono) creata dalla Coordinatrice per bloccare uno slot quando il paziente è nuovo e chiama per telefono; resta "provvisoria" finché il consenso privacy/dati sanitari non viene raccolto di persona.
- **Terapia manuale** — riabilitazione, rieducazione motoria, massoterapia, mobilizzazioni, manipolazioni, ginnastica posturale, drenaggio linfatico, secondo la tassonomia usata in questo progetto (ripresa da un modulo di consenso cartaceo reale, non pubblicato).
- **Terapia fisica strumentale** — applicazione di mezzi fisici: correnti elettriche (TENS, ionoforesi, diadinamica, elettrostimolazione), infrarossi, diatermia da contatto o capacitivo-resistiva, ipertermia, laser, magnetoterapia/S.I.S., ultrasuono terapia, pressoterapia intermittente. **È il medico a prescriverla**, quindi nel percorso convenzionato arriva dalla ricetta; nel percorso privato è pagata direttamente dal paziente.
- **VAS (scala analogica visiva)** — misura del dolore riferito dal paziente su scala 0-10, rilevata a inizio e a fine ciclo di trattamento. Unico dato clinico strutturato e confrontabile della scheda.
- **Sistema Tessera Sanitaria (Sistema TS)** — sistema del MEF a cui le strutture sanitarie trasmettono le spese sostenute dai cittadini, perché confluiscano nella dichiarazione dei redditi precompilata. Il paziente può **opporsi** all'invio: l'opposizione va raccolta e registrata al momento dell'incasso.
- **Imposta di bollo** — € 2,00 dovuti sui documenti relativi a operazioni esenti da IVA di importo superiore a € 77,47. Poiché le prestazioni sanitarie sono esenti IVA (art. 10 n. 18 DPR 633/72), il bollo scatta sulle ricevute sopra quella soglia.
- **Storno** — documento di pari importo e segno opposto che annulla una ricevuta già emessa. Una ricevuta non si cancella mai: la numerazione progressiva deve restare integra.

## Punti aperti da chiarire più avanti

**Chiusi lavorando sui mockup di design** (erano rimandati alla fase di UI per scelta esplicita)
- ~~Dettaglio della sezione Cassa~~ → definito sopra in "Sezione Cassa".
- ~~Dettaglio della validazione ricette SSN~~ → definito sopra in "Validazione delle ricette SSN".
- ~~Scadenza di una ricetta in stato "integrazione"~~ (10 settembre 2026): niente decadenza automatica dello slot — resta una decisione della segreteria. Ma non senza aiuto: dopo N giorni (soglia da confermare con lo studio, indicativamente 15-30) la ricetta sale di priorità nella coda Ricette SSN, così l'attesa non passa inosservata. Sotto soglia nessun segnale aggiuntivo, la coda ordina già per urgenza d'agenda.

**Aperto dal collegamento Richieste ↔ Ricette SSN**
- Nessuno, vedi sopra.

**Chiuso il 12 settembre 2026**
- ~~Quota ticket per ciclo riabilitativo in Regione Lazio~~ → piccolo listino nel sistema, precompilato ma sempre modificabile a mano in validazione: le quote regionali cambiano raramente, un listino riduce errori di trascrizione rispetto a inserirle ogni volta a mano, ma un valore mostrato come editabile non va mai trattato come bloccato. Valore provvisorio in uso: **36 €**, la quota che le ricette d'esempio portano già scritta — da riverificare con uno studio reale prima della produzione, e modificabile da Configurazione senza toccare il codice (vedi sotto).

**Aperti dalla definizione della Cassa**
- ~~Trasmissione al Sistema Tessera Sanitaria~~ → **chiuso il 12 settembre 2026**: nessuna integrazione diretta via API. Il sistema produce solo il file da caricare altrove, resta un adempimento operativo dello studio. Lo stato della ricevuta (da inviare / inviata / opposizione) resta comunque tracciato in Cassa, ma il "da inviare" segna solo che il file non è ancora stato generato/esportato, non un invio automatico.
- ~~Numerazione delle ricevute~~ → **chiuso il 12 settembre 2026**: progressiva annuale unica confermata (non per sezionale privato/SSN) — entrambi i percorsi hanno lo stesso trattamento fiscale (esenzione IVA art. 10 n. 18), niente obbligo che imponga sezionali separati. Resta comunque da confermare con uno studio reale/il proprio commercialista prima dello scaffolding: se la prassi attuale usa già sezionali distinti, cambiarla in corsa spezzerebbe la continuità della numerazione fiscale.

**Da verificare con uno studio reale, quando servirà**
- Finestra di completamento del ciclo secondo la normativa della Regione Lazio: i 30/60 giorni riportati in Glossario vengono da fonti generiche. Il tetto di 10 sedute per ciclo è invece verificato, e i limiti annui restano in carico alla segreteria, non al sistema.
- L'importo esatto della quota ticket regionale (36 € è un valore provvisorio) e la regola su cosa succede a una seduta in caso di no-show (dettagli in [DEVLOG.md](DEVLOG.md)).

**Nota sul nome dello studio**: questo documento e il resto del repository pubblico descrivono deliberatamente uno **studio generico**, non quello reale da cui il progetto è partito. Nome, logo, moduli cartacei e dati anagrafici reali sono rimasti in un repository privato; anche la demo pubblicata online non li mostra, per una ragione di sicurezza reale (un falso positivo di Google Safe Browsing — vedi [DEVLOG.md](DEVLOG.md)), non solo per la pubblicazione di questo repository.
