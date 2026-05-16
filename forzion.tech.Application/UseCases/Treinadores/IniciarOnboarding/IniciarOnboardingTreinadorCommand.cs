namespace forzion.tech.Application.UseCases.Treinadores.IniciarOnboarding;

public record IniciarOnboardingTreinadorCommand(
    Guid TreinadorId,
    string UrlRetorno,
    string UrlCancelamento);
