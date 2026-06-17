using forzion.tech.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Services;

public sealed class NullEmissorNfseService : IEmissorNfseService
{
    private const string CodigoDesabilitado = "NFSE_DESABILITADO";
    private const string MotivoDesabilitado = "Emissão de NFS-e desabilitada (Nfse:Habilitado=false).";

    private readonly ILogger<NullEmissorNfseService> _logger;

    public NullEmissorNfseService(ILogger<NullEmissorNfseService> logger)
    {
        _logger = logger;
        _logger.LogWarning("Emissor de NFS-e não configurado. Notas não serão emitidas.");
    }

    public Task<NfseResultado> EmitirAsync(DpsInput input, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("NFS-e desabilitada. DPS {NumeroDps} não emitida.", input.NumeroDpsEstavel);
        return Task.FromResult(new NfseResultado(false, null, null, null, null, CodigoDesabilitado, MotivoDesabilitado));
    }

    public Task<NfseStatus> ConsultarAsync(string chaveAcesso, CancellationToken cancellationToken = default) =>
        Task.FromResult(new NfseStatus(NfseSituacao.NaoEncontrada, null, null, null, CodigoDesabilitado, MotivoDesabilitado));

    public Task<NfseResultado> CancelarAsync(string chaveAcesso, string motivo, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("NFS-e desabilitada. Cancelamento de {ChaveAcesso} ignorado.", chaveAcesso);
        return Task.FromResult(new NfseResultado(false, null, null, null, null, CodigoDesabilitado, MotivoDesabilitado));
    }
}
