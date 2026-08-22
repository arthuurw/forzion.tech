using System.Diagnostics.CodeAnalysis;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Agents;

internal static class AgentTenantGuard
{
    // Colapso único de not-found + treinador inativo + perfil não-publicado: divergir confirmaria
    // a um chamador não autorizado que o tenant existe (mesma defesa IDOR de
    // DefinirPreservacaoVinculoHandler). Compartilhado entre todos os endpoints de leitura do
    // gateway de agentes para os 3 casos nunca divergirem por engano entre handlers.
    internal static bool EstaPublicado([NotNullWhen(true)] Treinador? treinador) =>
        treinador is not null && treinador.Status == TreinadorStatus.Ativo && treinador.PerfilPublico.IsPublicado;
}
