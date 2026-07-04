namespace forzion.tech.Application.Interfaces;

public interface IDigestTreinadorEmailNotifier
{
    Task NotificarAsync(Guid treinadorId, int treinaram, int naoTreinaram, CancellationToken cancellationToken = default);
}
