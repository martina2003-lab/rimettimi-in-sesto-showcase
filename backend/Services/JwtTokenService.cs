using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using RimettimiInSesto.Api.Models;

namespace RimettimiInSesto.Api.Services;

// RBAC nativo di ASP.NET Core Identity + JWT verso la SPA — vedi ARCHITETTURA.md, stack tecnico.
// Il ruolo va sempre nel token come claim standard ClaimTypes.Role: è quello che
// [Authorize(Roles = "...")] controlla lato server su ogni endpoint, non un dettaglio di UI.
public class JwtTokenService(IConfiguration configuration)
{
    private readonly string _key = configuration["Jwt:Key"]
        ?? "chiave-di-sviluppo-solo-demo-non-usare-in-produzione-32char+";

    private const int ScadenzaMinuti = 480; // 8 ore: comoda per una sessione di lavoro/demo

    public (string Token, DateTime ScadeIl) GeneraToken(ApplicationUser utente)
    {
        var scadeIl = DateTime.UtcNow.AddMinutes(ScadenzaMinuti);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, utente.Id),
            new(ClaimTypes.NameIdentifier, utente.Id),
            new(ClaimTypes.Email, utente.Email ?? string.Empty),
            new(ClaimTypes.GivenName, utente.Nome),
            new(ClaimTypes.Surname, utente.Cognome),
            new(ClaimTypes.Role, utente.Ruolo.ToString()),
        };

        var credenziali = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key)), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: claims,
            expires: scadeIl,
            signingCredentials: credenziali);

        return (new JwtSecurityTokenHandler().WriteToken(token), scadeIl);
    }
}
