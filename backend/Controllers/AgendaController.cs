using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RimettimiInSesto.Api.Services;

namespace RimettimiInSesto.Api.Controllers;

[ApiController]
[Route("api/agenda")]
[Authorize] // tutti e quattro i ruoli leggono la disponibilità (prenotazione, calendario, agenda propria)
public class AgendaController(AvailabilityService availabilityService) : ControllerBase
{
    // Un solo giorno: la griglia settimanale del frontend richiama questo endpoint una volta
    // per colonna, così ogni giorno resta cache-abile/ricalcolabile indipendentemente.
    [HttpGet("disponibilita")]
    public async Task<ActionResult<DisponibilitaGiornoResult>> Disponibilita(
        [FromQuery] int fisioterapistaId, [FromQuery] DateOnly data)
    {
        var risultato = await availabilityService.CalcolaDisponibilitaGiornoAsync(fisioterapistaId, data);
        return Ok(risultato);
    }

    // "Nessuna preferenza": requisiti.md prevede che il paziente possa non scegliere il
    // terapista e prendere la prima disponibilità. Sta qui e non nel client perché il
    // confronto è *fra* i terapisti: farlo di là vorrebbe dire una richiesta per ciascuno
    // e per ogni giorno.
    [HttpGet("prima-disponibilita")]
    public async Task<ActionResult<List<PrimaDisponibilita>>> PrimaDisponibilita(
        [FromQuery] int durataMinuti, [FromQuery] DateOnly? da, [FromQuery] int giorni = 21)
    {
        if (durataMinuti is not (30 or 60 or 90))
        {
            return BadRequest(new { messaggio = "La durata deve essere 30, 60 o 90 minuti." });
        }

        var partenza = da ?? DateOnly.FromDateTime(DateTime.Today);
        return Ok(await availabilityService.PrimeDisponibilitaAsync(
            durataMinuti, partenza, Math.Clamp(giorni, 1, 60), massimo: 12));
    }

    // Griglia libero/occupato di una settimana, tutti i fisioterapisti insieme: solo la
    // Coordinatrice cerca un orario così (Calendario "Tutti", Nuovo Appuntamento "Cerca per
    // orario") — non è disponibilità che un paziente debba vedere.
    [HttpGet("libero-occupato")]
    [Authorize(Roles = "Coordinatrice")]
    public async Task<ActionResult<List<GiornoLiberoOccupato>>> LiberoOccupato(
        [FromQuery] DateOnly da, [FromQuery] DateOnly a, [FromQuery] int durataMinuti)
    {
        if (durataMinuti is not (30 or 60 or 90))
        {
            return BadRequest(new { messaggio = "La durata deve essere 30, 60 o 90 minuti." });
        }
        if (a < da || a.DayNumber - da.DayNumber > 6)
        {
            return BadRequest(new { messaggio = "L'intervallo deve essere al massimo una settimana." });
        }

        return Ok(await availabilityService.GrigliaLiberoOccupatoAsync(da, a, durataMinuti));
    }
}
