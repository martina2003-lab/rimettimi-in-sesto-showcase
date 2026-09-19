# Devlog

Sintesi curata delle decisioni di progetto e dei problemi reali incontrati costruendo "Rimettimi in sesto" — non la cronaca giorno per giorno (quella resta in un repository privato, insieme ai materiali dello studio reale da cui il progetto è partito), ma i punti in cui una scelta, un bug o un incidente hanno cambiato qualcosa di non ovvio.

## Il progetto

Portale di prenotazione per uno studio di fisioterapia mono-tenant, privato ma accreditato per prestazioni convenzionate SSN. Quattro ruoli con permessi e viste distinti — Paziente, Fisioterapista, Coordinatrice, Admin — su un'unica agenda condivisa. Partito da mockup HTML navigabili (uno per ruolo, costruiti a partire dai moduli cartacei reali dello studio) e poi portato su un'applicazione vera: ASP.NET Core 8 + EF Core + SQLite sul backend, React + TypeScript + Tailwind sul frontend, pubblicata su Azure App Service.

Le decisioni strutturali sono descritte in [ARCHITETTURA.md](ARCHITETTURA.md) e [requisiti.md](requisiti.md). Qui restano il *come ci si è arrivati* e i bug che vale la pena raccontare.

## Principi che hanno guidato ogni scelta successiva

Tre decisioni prese presto hanno tenuto la barra dritta per tutto il resto del progetto:

- **L'accesso clinico deriva dall'appuntamento, non da un legame statico.** Non esiste una FK `Paziente.fisioterapistaDiRiferimento`. Un fisioterapista accede alla cartella di un paziente se ha, o ha avuto, un appuntamento assegnato con lui — verificato lato server ad ogni richiesta, mai in UI. Questo copre in modo uniforme sia i pazienti abituali sia le sostituzioni per assenza, senza bisogno di permessi temporanei da concedere e revocare a mano. Il "fisioterapista abituale" mostrato al paziente è un valore *calcolato* dallo storico appuntamenti (più di un appuntamento **e** più di ogni collega — una regola che è servita a non far risultare "abituali" due terapisti diversi per lo stesso paziente).
- **Ogni accesso concesso scrive un audit log; un accesso negato non scrive nulla**, perché nessun accesso è avvenuto. È l'unico meccanismo di controllo su un'autorizzazione che è derivata, non esplicita.
- **Nessuna prenotazione si auto-conferma**, tranne quelle che nascono già confermate perché le crea la Coordinatrice stessa (telefono/sportello) — è lei il confermatore. Il calcolo della disponibilità considera occupati anche gli slot "richiesti" e quelli dentro una proposta di orario alternativo ancora aperta: altrimenti la conferma manuale diventerebbe una corsa fra pazienti sullo stesso orario.

## Il bug che si è ripetuto nove volte

Il difetto più istruttivo del progetto non è un singolo bug, ma un pattern che è tornato **nove volte** in punti diversi del backend: un campo esiste nel modello, sembra significare qualcosa, e non è collegato a nessuna logica — né in lettura né in scrittura. Esempi reali:

- `SeduteResidue` di un pacchetto non veniva mai decrementato: un pacchetto da 10 sedute restava a 10 per sempre, perché nessuno chiudeva mai una seduta (`Completato` esisteva solo nel seed).
- La finestra massima di prenotazione (60 giorni) esisteva come colonna in tabella ma non era applicata da nessun controllo.
- `TipoNotifica.AvvisoDisponibilitaAnticipata` era nell'enum dal primo giorno e non veniva mai emesso da nessun endpoint.
- `TipoPrestazione` era descritto in architettura come la dimensione che incrocia le entrate dell'Admin, ma non stava su nessuna entità — la vista centrale del titolare era semplicemente impossibile da costruire finché non è stato aggiunto.
- Un'intera entità, `Consenso`, esisteva dal primo giorno con i campi giusti (versione dell'informativa, firmatario) e zero endpoint, zero schermate, zero righe nel seed: lo stato "consenso raccolto" della scheda paziente era un'etichetta che niente poteva far cambiare.

La lezione operativa, utile oltre questo progetto: **cercare i campi mai scritti conviene farlo apposta**, con un grep di chi *scrive* un campo (non di chi lo legge), invece di aspettare che il buco salti fuori da solo. Da un certo punto in poi è diventato un controllo di routine dopo ogni nuova entità.

## Bug trovati sfruttandoli davvero, non leggendo il codice

Una revisione mirata del backend ha cercato di rompere l'autorizzazione lato server chiamando gli endpoint via HTTP con dati creati ad hoc, non rileggendo il codice a occhio. Quattro bug reali:

1. Si poteva addebitare una seduta al **pacchetto di un altro paziente**.
2. Si poteva prenotare SSN sulla **ricetta di un altro paziente**.
3. Si poteva prenotare **nel passato**.
4. La finestra massima di prenotazione (60 giorni) non era applicata (vedi sopra).

La radice comune dei primi due: il controllo di titolarità c'era sul `PazienteId` della richiesta, ma **le risorse collegate (pacchetto, ricetta) hanno un proprietario proprio**, e nessuno lo verificava. Corretto al centro, nel servizio di prenotazione condiviso da booking online e telefonico — non ai margini, dove sarebbe rimasto vero solo per un percorso.

Un quinto sospetto si è rivelato **falso allarme**, verificato eseguendolo invece di darlo per scontato: un pattern che sembrava un crash sicuro con EF Core (una proiezione con un metodo statico) è in realtà ammesso nella valutazione client-side finale di una query.

## Il ciclo di vita di un appuntamento aveva due buchi più grossi delle sue lacune dichiarate

Chiudendo le lacune note del ciclo di vita (modifica, cancellazione, proposta di slot alternativo) sono emersi due problemi strutturali, entrambi del genere "campo mai collegato": nessuno chiudeva mai una seduta, e il pacchetto non scalava mai. Corretti insieme, con una scelta esplicita che vale la pena annotare: **una seduta completata è l'unico momento in cui il pacchetto cala** — non alla prenotazione (ancora annullabile) né alla conferma. Il no-show, deliberatamente, non scala nulla: se una seduta mancata vada persa o recuperata cambia tra percorso privato e SSN, e nessun documento raccolto lo chiariva — meglio lasciarlo fuori che inventare una regola.

## Race condition su prenotazioni simultanee

Una race condition dichiarata "accettabile per una demo" è stata poi chiusa: fra "lo slot è libero?" e "scrivo l'appuntamento" non c'era nulla a impedire che due richieste sullo stesso slot passassero entrambe il controllo. Corretta con una sezione critica in-process più un indice unico su (fisioterapista, data/ora) per gli stati attivi, come rete a livello di database. Il lucchetto è di processo — vale finché l'API gira su una sola istanza — e copre la collisione esatta, non le sovrapposizioni parziali (per quelle resta la sezione critica; un vincolo di esclusione su intervalli non è disponibile su SQLite). Verificato sparando cinque prenotazioni simultanee sullo stesso slot: ne passa una.

## I bug che l'API non poteva vedere

Due categorie di difetti sono emerse solo usando l'applicazione vera dal browser, mai dai test sull'API:

- **Un valore sbagliato nasceva nel client**: il wizard di prenotazione del Paziente mandava sempre `durataMinuti: null` per un percorso SSN senza ricetta, mentre il backend — corretto in quel caso — pretende un valore quando non ci sono distretti da cui calcolarlo. Il valore giusto il client lo calcolava già per mostrarlo a schermo, semplicemente non lo stava inviando. Lo stesso scenario, nel flusso di prenotazione telefonica della Coordinatrice, era corretto: due percorsi diversi per lo stesso caso, uno giusto e uno no.
- **Gestione della sessione**: staccando il backend con l'interfaccia aperta, un errore di rete ("Failed to fetch", messaggio del browser non tradotto) veniva trattato come sessione scaduta — l'utente veniva sloggato e il token cancellato, quando sarebbe bastato un calo di connessione a costringere a un login con credenziali ancora valide. Corretto distinguendo esplicitamente i due casi: un 401 vero fa uscire, un errore di rete tiene la sessione e offre "Riprova". Un secondo difetto collegato: l'endpoint di verifica sessione leggeva solo i claim del token JWT senza controllare che l'utente esistesse ancora nel database — con un database ripartito da capo, un token tecnicamente valido ma orfano produceva un 403 sulla prima chiamata ai dati invece di un pulito 401 che rimandasse al login.

## Ridisegnare una dashboard: dal "sembra scarno" ai dati veri

La vista di andamento economico dell'Admin è passata da un primo tentativo scarno a una versione con più dati, dopo un giro esplicito di *design review* — guardare solo lo schermo, senza leggere codice, chiedendosi come la userebbe davvero chi gestisce un'impresa. Sono emersi problemi concreti: un grafico a torta che non diceva gli importi in euro, barre dei mesi futuri completamente mute invece di mostrare cosa fosse già confermato in agenda, nessun confronto con l'anno precedente. Due bug di contrasto trovati provando la preview, non leggendo il codice: barre a valore zero dello stesso colore dello sfondo (quindi invisibili), ed etichette di variazione che si sovrapponevano quando tre mesi consecutivi mostravano un valore.

Interessante anche una scelta di cosa **non** mostrare: la percentuale di variazione è stata scartata a favore del valore assoluto in euro, perché con un database dimostrativo che parte a zero un confronto percentuale produce solo divisioni per zero o crescite "infinite" prive di significato — l'euro resta un numero vero anche quando il periodo precedente non ha storico.

Una vista più piccola ("Entrate") è stata poi assorbita in questa, dopo aver notato che duplicava esattamente l'incrocio privato/SSN × manuale/strumentale che l'altra vista offriva già a grana annuale — la stessa lezione del "non costruire due volte la stessa risposta" vista anche nella gestione delle code Richieste/Ricette.

## Un incidente di sicurezza reale sulla demo pubblicata

Poco dopo la pubblicazione su Azure, il browser ha iniziato a mostrare un avviso "sito pericoloso" (Google Safe Browsing, classificazione phishing) aprendo la demo. Prima di tutto è stata esclusa un'intrusione, verificandolo invece di darlo per scontato: pubblicazioni provenienti solo dall'account e dal repository proprietari, codice servito online identico a quello sorgente (nessuno script iniettato, nessuna chiamata verso domini terzi), nessun file sensibile raggiungibile, ogni endpoint protetto rispondeva correttamente senza credenziali. Nessuna violazione: **falso positivo**, ma non casuale.

La causa era una combinazione di segnali che i classificatori antifrode leggono esattamente come una pagina-clone: un modulo email/password, il marchio di un'attività sanitaria reale, un indirizzo gratuito e condiviso (`*.azurewebsites.net`, dominio pesantemente abusato per phishing) che con quel marchio non c'entrava nulla — più un'incoerenza visiva che peggiorava il quadro (un logo non ancora aggiornato al nome corrente del progetto). Marchio incoerente più raccolta di credenziali su dominio condiviso è, letteralmente, il profilo di una pagina di phishing.

Il rimedio, applicato solo alla versione pubblicata (non ai materiali di design, rimasti privati): un nome di progetto neutro senza legami con un'attività esistente, un simbolo astratto disegnato apposta al posto del logo reale, l'eliminazione del modulo email/password (si entra scegliendo un ruolo, niente da digitare), una dichiarazione esplicita "demo" visibile in ogni pagina, account demo su un dominio email riservato agli esempi (`@studio.example`, che non può esistere davvero), e il tema scuro disattivato di default perché la demo apparisse identica a chiunque la aprisse. La segnalazione è stata poi rimossa da Google dopo revisione. Il rimedio strutturale — un dominio proprio, per uscire dalla reputazione condivisa di `*.azurewebsites.net` — resta una decisione di costo non urgente, non più bloccante ora che la segnalazione è rientrata.

## Filosofia di testing

Non una suite esaustiva: per un portale dimostrativo il resto si verifica a mano. Test automatici concentrati sul codice dove un errore costerebbe caro — la regola di accesso clinico e il suo audit log, le autorizzazioni di prenotazione, i comportamenti del ciclo di vita che sarebbe facile rompere "semplificando" (chi può scalare cosa, chi può vedere cosa). Una verifica end-to-end separata, ripetuta periodicamente attraversando i quattro ruoli in sequenza sullo stesso dato condiviso, ha trovato difetti che nessun test unitario avrebbe potuto vedere: quelli di gestione della sessione descritti sopra si manifestano solo staccando il backend con l'interfaccia già aperta, non in una chiamata isolata.
