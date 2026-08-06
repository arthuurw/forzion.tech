using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using forzion.tech.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using DadosFiscaisVo = forzion.tech.Domain.ValueObjects.DadosFiscais;

namespace forzion.tech.Application.UseCases.Treinadores.DadosFiscais;

public class DefinirDadosFiscaisTreinadorHandler(
    ITreinadorRepository treinadorRepository,
    ILogAprovacaoRepository logRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<DefinirDadosFiscaisTreinadorHandler> logger,
    IUserContext userContext)
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

        var logResult = await logRepository.RegistrarAsync(
            TipoAcaoAprovacao.DefinicaoDadosFiscaisTreinador,
            userContext.PerfilId,
            treinador.Id,
            nameof(Treinador),
            agora,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (logResult.IsFailure)
            return Result.Failure<DadosFiscaisResponse>(logResult.Error!);

        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Dados fiscais definidos para treinador {TreinadorId} por {AtorId}.", treinador.Id, userContext.PerfilId);

        return Result.Success(MapResponse(dadosResult.Value));
    }

    internal static DadosFiscaisResponse MapResponse(DadosFiscaisVo dados) =>
        new(dados.TipoDocumento, dados.Documento, dados.RazaoSocial, dados.InscricaoMunicipal,
            new EnderecoFiscalResponse(
                dados.Endereco.Logradouro, dados.Endereco.Numero, dados.Endereco.Complemento,
                dados.Endereco.Bairro, dados.Endereco.CodigoMunicipioIbge, dados.Endereco.Uf, dados.Endereco.Cep));
}
