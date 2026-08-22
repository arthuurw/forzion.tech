using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Application.UseCases.Agents.BusinessInfo;

public class ObterBusinessInfoHandler(
    ITreinadorRepository treinadorRepository,
    IPacoteRepository pacoteRepository)
{
    public virtual Task<Result<BusinessInfoResponse>> HandleAsync(
        ObterBusinessInfoQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return HandleAsyncCore(query, cancellationToken);
    }

    private async Task<Result<BusinessInfoResponse>> HandleAsyncCore(
        ObterBusinessInfoQuery query,
        CancellationToken cancellationToken)
    {
        var treinador = await treinadorRepository.ObterPorIdAsync(query.TenantId, cancellationToken).ConfigureAwait(false);

        // Not-found, treinador inativo e perfil não-publicado colapsam no MESMO erro: divergir
        // confirmaria a um chamador não autorizado que o tenant existe (mesma defesa IDOR de
        // DefinirPreservacaoVinculoHandler).
        if (treinador is null || treinador.Status != TreinadorStatus.Ativo || !treinador.PerfilPublico.IsPublicado)
            return Result.Failure<BusinessInfoResponse>(TreinadorErrors.NaoEncontrado);

        var pacotesPublicos = await pacoteRepository
            .ListarPublicosPorTreinadorAsync(query.TenantId, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(MapResponse(treinador.PerfilPublico, DerivarModalidades(pacotesPublicos)));
    }

    internal static IReadOnlyList<string> DerivarModalidades(IReadOnlyList<Pacote> pacotesPublicos)
    {
        var vistas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var modalidades = new List<string>();

        foreach (var categoria in pacotesPublicos.Select(pacote => pacote.Categoria))
        {
            if (string.IsNullOrWhiteSpace(categoria))
                continue;
            if (vistas.Add(categoria))
                modalidades.Add(categoria);
        }

        return modalidades;
    }

    internal static BusinessInfoResponse MapResponse(PerfilPublico perfil, IReadOnlyList<string> modalidades) =>
        new(
            perfil.NomeFantasia!,
            perfil.Endereco is null ? null : MapAddress(perfil.Endereco),
            modalidades,
            perfil.HorariosFuncionamento.Count == 0 ? null : perfil.HorariosFuncionamento.Select(MapOpeningHours).ToList(),
            perfil.Politicas is null || perfil.Politicas.Count == 0 ? null : perfil.Politicas);

    private static AddressResponse MapAddress(EnderecoPublico endereco) =>
        new(endereco.Rua, endereco.Numero, endereco.Complemento, endereco.Bairro, endereco.Cidade, endereco.Estado, endereco.Cep);

    private static OpeningHoursResponse MapOpeningHours(HorarioFuncionamento horario) =>
        new(horario.DiaSemana, horario.AbreAs.ToString("HH:mm"), horario.FechaAs.ToString("HH:mm"));
}
