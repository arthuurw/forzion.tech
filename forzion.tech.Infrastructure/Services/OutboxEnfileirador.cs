using System.Text.Json;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;

namespace forzion.tech.Infrastructure.Services;

public sealed class OutboxEnfileirador(IOutboxRepository repository, TimeProvider timeProvider) : IOutboxEnfileirador
{
    public void Enfileirar<TPayload>(string tipo, TPayload payload, string chaveIdempotencia)
    {
        var json = JsonSerializer.Serialize(payload);
        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var resultado = OutboxEfeito.Criar(tipo, json, chaveIdempotencia, agora);

        // Falha aqui é erro de programação (tipo/chave vazios), não de runtime — não há
        // caminho de recuperação; estoura para o dev em vez de engolir efeito silenciosamente.
        if (resultado.IsFailure)
            throw new InvalidOperationException($"Efeito outbox inválido: {resultado.Error!.Message}");

        repository.Enfileirar(resultado.Value);
    }
}
