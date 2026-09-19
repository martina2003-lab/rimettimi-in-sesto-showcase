using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RimettimiInSesto.Api.Dtos;
using RimettimiInSesto.Api.Models;
using RimettimiInSesto.Api.Services;

namespace RimettimiInSesto.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(UserManager<ApplicationUser> userManager, JwtTokenService jwtTokenService)
    : ControllerBase
{
    // Login demo: nessun account reale, nessun invio email — solo le 4 credenziali di
    // mockup/login.html (più i fisioterapisti aggiuntivi del seed). Vedi ARCHITETTURA.md, note demo.
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var utente = await userManager.FindByEmailAsync(request.Email);
        if (utente is null || !await userManager.CheckPasswordAsync(utente, request.Password))
        {
            return Unauthorized(new { messaggio = "Email o password non corretti." });
        }

        var (token, scadeIl) = jwtTokenService.GeneraToken(utente);

        return Ok(new LoginResponse(
            token, scadeIl, utente.Id, utente.Email!, utente.Nome, utente.Cognome, utente.Ruolo.ToString()));
    }

    // Verifica della sessione al ricaricamento della pagina. Non si limita a decodificare
    // i claim: controlla che l'utente esista ancora. Un token può restare valido (firma e
    // scadenza a posto) mentre l'account dietro non c'è più — account rimosso, oppure un
    // database ripartito da capo. Senza questo controllo la SPA monta l'area del ruolo e
    // scopre il problema solo quando la prima chiamata ai dati risponde 403, che per chi
    // guarda sembra un guasto invece di una sessione da rifare.
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<MeResponse>> Me()
    {
        var utenteId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        var utente = await userManager.FindByIdAsync(utenteId);
        if (utente is null)
        {
            return Unauthorized(new { messaggio = "Sessione non più valida, accedi di nuovo." });
        }

        return Ok(new MeResponse(
            utente.Id, utente.Email ?? string.Empty, utente.Nome, utente.Cognome, utente.Ruolo.ToString()));
    }
}
