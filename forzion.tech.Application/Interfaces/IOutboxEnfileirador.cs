namespace forzion.tech.Application.Interfaces;

public interface IOutboxEnfileirador
{
    // Serializa o payload e adiciona o efeito ao MESMO UnitOfWork em curso — persiste
    // junto do agregado no próximo SaveChanges (atomicidade; sem commit próprio).
    void Enfileirar<TPayload>(string tipo, TPayload payload, string chaveIdempotencia);
}
