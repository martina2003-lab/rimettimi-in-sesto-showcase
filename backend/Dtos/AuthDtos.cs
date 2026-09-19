namespace RimettimiInSesto.Api.Dtos;

public record LoginRequest(string Email, string Password);

public record LoginResponse(
    string Token,
    DateTime ScadeIl,
    string UtenteId,
    string Email,
    string Nome,
    string Cognome,
    string Ruolo);

public record MeResponse(
    string UtenteId,
    string Email,
    string Nome,
    string Cognome,
    string Ruolo);
