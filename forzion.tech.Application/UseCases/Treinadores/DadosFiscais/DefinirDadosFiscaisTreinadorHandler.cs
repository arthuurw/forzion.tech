using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using forzion.tech.Domain.ValueObjects;
using DadosFiscaisVo = forzion.tech.Domain.ValueObjects.DadosFiscais;

namespace forzion.tech.Application.UseCases.Treinadores.DadosFiscais;

public class DefinirDadosFiscaisTreinadorHandler(
    ITreinadorRepository treinadorRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public virtual async Task<Result<DadosFiscaisResponse>> HandleAsync(
        DefinirDadosFiscaisTreinadorCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var treinador = await treinadorRepository.ObterPorIdAsync(command.TreinadorId, cancellationToken).ConfigureAwait(false);
        if (treinador is null)
            return Result.Failure<DadosFiscaisResponse>(TreinadorErrors.NaoEncontrado);

        var enderecoResult = EnderecoFiscal.Criar(
            command.Logradouro, command.Numero, command.Bairro,
            command.CodigoMunicipioIbge, command.Uf, command.Cep, command.Complemento);
        if (enderecoResult.IsFailure)
            return Result.Failure<DadosFiscaisResponse>(enderecoResult.Error!);

        var dadosResult = DadosFiscaisVo.Criar(
            command.TipoDocumento, command.Documento, command.RazaoSocial, enderecoResult.Value, command.InscricaoMunicipal);
        if (dadosResult.IsFailure)
            return Result.Failure<DadosFiscaisResponse>(dadosResult.Error!);

        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var definir = treinador.DefinirDadosFiscais(dadosResult.Value, agora);
        if (definir.IsFailure)
            return Result.Failure<DadosFiscaisResponse>(definir.Error!);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(MapResponse(dadosResult.Value));
    }

    internal static DadosFiscaisResponse MapResponse(DadosFiscaisVo dados) =>
        new(dados.TipoDocumento, dados.Documento, dados.RazaoSocial, dados.InscricaoMunicipal,
            new EnderecoFiscalResponse(
                dados.Endereco.Logradouro, dados.Endereco.Numero, dados.Endereco.Complemento,
                dados.Endereco.Bairro, dados.Endereco.CodigoMunicipioIbge, dados.Endereco.Uf, dados.Endereco.Cep));
}
