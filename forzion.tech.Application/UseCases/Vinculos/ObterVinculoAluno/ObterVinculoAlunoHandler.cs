using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Application.UseCases.Vinculos.ObterVinculoAluno;

public record VinculoAlunoItemResponse(
    Guid VinculoId,
    Guid TreinadorId,
    string NomeTreinador,
    VinculoStatus Status,
    DateTime? DataInicio,
    DateTime CreatedAt);

public record ObterVinculoAlunoResponse(
    VinculoAlunoItemResponse? VinculoAtivo,
    VinculoAlunoItemResponse? VinculoPendente);

public class ObterVinculoAlunoHandler(
    IVinculoTreinadorAlunoRepository vinculoRepository,
    ITreinadorRepository treinadorRepository)
{
    public virtual async Task<ObterVinculoAlunoResponse> HandleAsync(
        Guid alunoId,
        CancellationToken cancellationToken = default)
    {
        var vinculoAtivo = await vinculoRepository.ObterAtivoPorAlunoAsync(alunoId, cancellationToken).ConfigureAwait(false);
        var vinculoPendente = await vinculoRepository.ObterPendentePorAlunoAsync(alunoId, cancellationToken).ConfigureAwait(false);

        VinculoAlunoItemResponse? ativoDto = null;
        if (vinculoAtivo is not null)
        {
            var treinador = await treinadorRepository.ObterPorIdAsync(vinculoAtivo.TreinadorId, cancellationToken).ConfigureAwait(false);
            ativoDto = new VinculoAlunoItemResponse(
                vinculoAtivo.Id, vinculoAtivo.TreinadorId,
                treinador?.Nome ?? "—",
                vinculoAtivo.Status, vinculoAtivo.DataInicio, vinculoAtivo.CreatedAt);
        }

        VinculoAlunoItemResponse? pendenteDto = null;
        if (vinculoPendente is not null)
        {
            var treinador = await treinadorRepository.ObterPorIdAsync(vinculoPendente.TreinadorId, cancellationToken).ConfigureAwait(false);
            pendenteDto = new VinculoAlunoItemResponse(
                vinculoPendente.Id, vinculoPendente.TreinadorId,
                treinador?.Nome ?? "—",
                vinculoPendente.Status, vinculoPendente.DataInicio, vinculoPendente.CreatedAt);
        }

        return new ObterVinculoAlunoResponse(ativoDto, pendenteDto);
    }
}
