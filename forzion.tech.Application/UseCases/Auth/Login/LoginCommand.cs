namespace forzion.tech.Application.UseCases.Auth.Login;

public record LoginCommand(string Email, string Senha, string? Rotulo = null);
