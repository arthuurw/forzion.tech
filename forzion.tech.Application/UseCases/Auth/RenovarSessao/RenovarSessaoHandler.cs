using System.Diagnostics;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.UseCases.Auth.RenovarSessao;

public class RenovarSessaoHandler(
    IRefreshTokenService refreshTokenService,
    IJwtService jwtService,
    IAlunoRepository alunoRepository,
    ITreinadorRepository treinadorRepository,
    ISystemUserRepository systemUserRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<RenovarSessaoHandler> logger)
{
    public virtual Task<Result<RenovarSessaoResponse>> HandleAsync(
        RenovarSessaoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return HandleAsyncCore(command, cancellationToken);
    }

    private async Task<Result<RenovarSessaoResponse>> HandleAsyncCore(
        RenovarSessaoCommand command,
        CancellationToken cancellationToken)
    {
        var agora = timeProvider.GetUtcNow().UtcDateTime;
        var rotacao = await refreshTokenService.RotacionarAsync(command.RefreshToken, agora, cancellationToken).ConfigureAwait(false);

        switch (rotacao.Resultado)
        {
            case ResultadoRotacao.Invalido:
                logger.LogWarning("Renovação de sessão negada — refresh token inválido.");
                return Result.Failure<RenovarSessaoResponse>(RefreshErrors.SessaoInvalida);

            case ResultadoRotacao.ReuseDetectado:
                // A família foi revogada in-memory pelo serviço; persistir a revogação.
                await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
                logger.LogWarning("Reuso de refresh token detectado na renovação — família {FamiliaId} revogada.", rotacao.FamiliaId);
                return Result.Failure<RenovarSessaoResponse>(RefreshErrors.SessaoInvalida);

            case ResultadoRotacao.Sucesso:
                return await EmitirAccessAsync(rotacao, cancellationToken).ConfigureAwait(false);

            default:
                // Enum exaustivo: cair aqui = valor novo não tratado. Falha alto em vez de
                // mascarar como SessaoInvalida (que esconderia o bug de um case faltando).
                throw new UnreachableException($"ResultadoRotacao não tratado: {rotacao.Resultado}");
        }
    }

    private async Task<Result<RenovarSessaoResponse>> EmitirAccessAsync(RotacaoResultado rotacao, CancellationToken cancellationToken)
    {
        var conta = rotacao.Conta!;
        var perfil = await ResolverPerfilAsync(conta, cancellationToken).ConfigureAwait(false);
        if (perfil is null)
        {
            // Perfil sumiu/bloqueado (ex.: treinador inativado) após a sessão criada:
            // não reemite access — força re-login. NÃO revoga (refresh segue válido até idle).
            logger.LogWarning("Renovação negada — perfil indisponível p/ conta {ContaId}.", conta.Id);
            return Result.Failure<RenovarSessaoResponse>(RefreshErrors.SessaoInvalida);
        }

        var (perfilId, nome) = perfil.Value;
        // Reemite o access mantendo a claim `fam` da mesma família (logout segue revogando este device).
        var token = jwtService.GerarToken(conta, perfilId, nome, rotacao.FamiliaId);
        await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new RenovarSessaoResponse(
            token, rotacao.RefreshRaw!, conta.TipoConta, conta.Id, perfilId, nome));
    }

    private async Task<(Guid PerfilId, string Nome)?> ResolverPerfilAsync(Domain.Entities.Conta conta, CancellationToken cancellationToken)
    {
        switch (conta.TipoConta)
        {
            case TipoConta.Aluno:
                var aluno = await alunoRepository.ObterPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false);
                return aluno is null ? null : (aluno.Id, aluno.Nome);

            case TipoConta.Treinador:
                var treinador = await treinadorRepository.ObterPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false);
                // Só treinador Ativo renova: um treinador inativado/reprovado perde a sessão.
                if (treinador is null || treinador.Status != TreinadorStatus.Ativo)
                    return null;
                return (treinador.Id, treinador.Nome);

            case TipoConta.SystemAdmin:
                var admin = await systemUserRepository.ObterPorContaIdAsync(conta.Id, cancellationToken).ConfigureAwait(false);
                return admin is null ? null : (admin.Id, admin.Nome);

            default:
                return null;
        }
    }
}
