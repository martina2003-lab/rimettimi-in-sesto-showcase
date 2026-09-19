using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RimettimiInSesto.Api.Data;
using RimettimiInSesto.Api.Models;
using RimettimiInSesto.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    // Enum come stringhe leggibili nell'API ("Privato", non "0") — coerente con la scelta
    // di salvarli come stringa nel DB (vedi ApplicationDbContext).
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter()));
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// SQLite anche in produzione, per scelta (ARCHITETTURA.md, "Note per il deploy demo"):
// per una demo evita il costo di Azure SQL, il lavoro di rigenerare le migration per un
// altro provider e soprattutto l'attesa del risveglio da auto-pausa, che sul primo accesso
// farebbe aspettare chi sta guardando.
//
// Su App Service il percorso persistente è /home: un file lì sopravvive a riavvii e nuovi
// deploy. Il valore arriva dalla configurazione così l'ambiente lo decide senza toccare il codice.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=rimettimiinsesto.db";

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        // Requisiti minimi per un demo: niente conferma email, niente lockout aggressivo.
        // Password semplici ("demo1234") perché sono già mostrate in chiaro in mockup/login.html —
        // coerente solo perché questa è la versione demo, non una policy da portare in produzione.
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager();

// In sviluppo la chiave arriva da `dotnet user-secrets` e c'è un ripiego per non bloccare
// chi clona il repo. In produzione no: senza una chiave vera l'applicazione non parte, e non
// riparte in silenzio con quella di sviluppo — che sta scritta qui sotto, quindi la conosce
// chiunque legga il codice, e con quella chiunque potrebbe firmarsi un token da Coordinatrice.
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "Jwt:Key non configurata. Su App Service va impostata come impostazione applicativa "
            + "Jwt__Key (idealmente con riferimento a Key Vault): senza, i token sarebbero firmati "
            + "con la chiave di sviluppo, che è pubblica nel codice sorgente.");
    }

    jwtKey = "chiave-di-sviluppo-solo-demo-non-usare-in-produzione-32char+";
}

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<ClinicalAccessService>();
builder.Services.AddScoped<AvailabilityService>();
builder.Services.AddScoped<NotificaService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<PropostaSlotService>();
builder.Services.AddScoped<RicettaService>();
builder.Services.AddScoped<PagamentoService>();

// CORS serve solo in sviluppo, dove Vite gira su una porta diversa. In produzione
// backend e frontend stanno sullo stesso App Service, quindi stessa origine e nessun
// permesso da concedere — vedi ARCHITETTURA.md, "Note per il deploy demo".
//
// Qualunque origine è ammessa (non solo localhost:5173): in sviluppo capita di aprire il
// portale anche dall'indirizzo IP del computer, per provarlo da un altro dispositivo sulla
// stessa rete (es. il telefono) — un'origine bloccata sul solo "localhost" lo impedirebbe.
// Nessun rischio: fuori da sviluppo questo blocco non esiste, e in produzione non c'è
// nessuna origine da concedere.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
        options.AddPolicy("Frontend", policy =>
            policy.SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod()));
}

var app = builder.Build();

// Applica le migration e precarica i dati demo — vedi ARCHITETTURA.md, "Note per il deploy demo":
// utenti/pazienti/appuntamenti precaricati invece di un flusso di registrazione reale.
await SeedData.SeedAsync(app.Services);

// SQLite apre i database in modalità WAL, che è più veloce ma ha bisogno di memoria
// condivisa fra i processi: sulle condivisioni di rete non c'è, e su App Service la
// cartella persistente /home è esattamente una condivisione di rete. Fuori da sviluppo si
// torna quindi al journal classico, che lì funziona. La modalità è scritta nel file del
// database e resta: basta impostarla una volta all'avvio.
if (!app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = DELETE;");
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("Frontend");
}

// Niente UseHttpsRedirection: su App Service il TLS lo termina Azure a monte e inoltra
// in HTTP, quindi un redirect qui provocherebbe un ciclo. HTTPS si impone dall'esterno
// con l'impostazione "HTTPS Only" dell'App Service, che è il posto giusto per farlo.

// Il frontend React compilato viene servito da qui: un solo App Service invece di due
// risorse separate, come deciso per contenere i costi della demo.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Le rotte del router React non esistono lato server: qualunque percorso non trovato va
// restituito come index.html, perché sia la SPA a risolverlo. Escluso /api, altrimenti
// un endpoint inesistente risponderebbe 200 con una pagina HTML invece di un 404 onesto.
app.MapFallback(async context =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    var indice = Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "index.html");
    if (!File.Exists(indice))
    {
        // Tipico in sviluppo, dove il frontend gira per conto suo su Vite.
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(indice);
});

app.Run();
