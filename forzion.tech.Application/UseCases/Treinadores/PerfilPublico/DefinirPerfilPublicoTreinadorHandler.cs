using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using EnderecoPublicoVo = forzion.tech.Domain.ValueObjects.EnderecoPublico;

namespace forzion.tech.Application.UseCases.Treinadores.PerfilPublico;

public class DefinirPerfilPublicoTreinadorHandler(
    ITreinadorRepository treinadorRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public virtual async Task<Result<PerfilPublicoResponse>> HandleAsync(
        DefinirPerfilPublicoTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var treinador = await treinadorRepository.ObterPorIdAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false);
        if (treinador is null)
            return Result.Failure<PerfilPublicoResponse>(TreinadorErrors.NaoEncontrado);

        EnderecoPublicoVo? endereco = null;
        if (command.Endereco is not null)
        {
            var enderecoResult = EnderecoPublicoVo.Criar(
                command.Endereco.Rua, command.Endereco.Bairro, command.Endereco.Cidade,
                command.Endereco.Estado, command.Endereco.Cep, command.Endereco.Numero, command.Endereco.Complemento);
            if (enderecoResult.IsFailure)
                return Result.Failure<PerfilPublicoResponse>(enderecoResult.Error!);
            endereco = enderecoResult.Value;
        }

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var perfil = treinador.PerfilPublico;

        var atualizarResult = perfil.AtualizarDados(command.NomeFantasia, endereco, command.Politicas, agora);
        if (atualizarResult.IsFailure)
            return Result.Failure<PerfilPublicoResponse>(atualizarResult.Error!);

        var horariosResult = perfil.SubstituirHorarios(
            [.. command.Horarios.Select(h => (h.DiaSemana, h.AbreAs, h.FechaAs))], agora);
        if (horariosResult.IsFailure)
            return Result.Failure<PerfilPublicoResponse>(horariosResult.Error!);

        if (command.IsPublicado)
        {
            var publicarResult = perfil.Publicar(agora);
            if (publicarResult.IsFailure)
                return Result.Failure<PerfilPublicoResponse>(publicarResult.Error!);
        }
        else
        {
            perfil.Despublicar(agora);
        }

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(MapResponse(perfil));
    }

    internal static PerfilPublicoResponse MapResponse(Domain.Entities.PerfilPublico perfil) => new(
        perfil.NomeFantasia,
        perfil.Endereco is null ? null : new EnderecoPublicoResponse(
            perfil.Endereco.Rua, perfil.Endereco.Numero, perfil.Endereco.Complemento,
            perfil.Endereco.Bairro, perfil.Endereco.Cidade, perfil.Endereco.Estado, perfil.Endereco.Cep),
        [.. perfil.HorariosFuncionamento.Select(h => new HorarioFuncionamentoResponse(
            h.Id, h.DiaSemana, h.AbreAs.ToString("HH:mm"), h.FechaAs.ToString("HH:mm")))],
        perfil.Politicas,
        perfil.IsPublicado);
}
