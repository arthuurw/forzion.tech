using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Application.Services;

public enum ResultadoCriacaoAssinaturaAluno { Criada, SemPacote, PacoteIndisponivel, DadosInvalidos }

public class CriarAssinaturaAlunoService(
    IPacoteRepository pacoteRepository,
    IAssinaturaAlunoRepository assinaturaRepository,
    ILogger<CriarAssinaturaAlunoService> logger)
{
    // Cria a AssinaturaAluno (Pendente) do vínculo e a adiciona ao repositório — o caller é dono do commit.
    // Não cria para pacote ausente/inativo ou preço inválido (Criar exige valor>0).
    // suprimirNotificacao=true descarta os eventos de domínio (bulk administrativo, sem e-mail/WhatsApp por-aluno).
    public virtual async Task<ResultadoCriacaoAssinaturaAluno> CriarParaVinculoAsync(
        VinculoTreinadorAluno vinculo, DateTime agora, bool suprimirNotificacao, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vinculo);

        if (vinculo.PacoteId is null)
            return ResultadoCriacaoAssinaturaAluno.SemPacote;

        var pacote = await pacoteRepository.ObterPorIdAsync(vinculo.PacoteId.Value, cancellationToken).ConfigureAwait(false);
        if (pacote is null || !pacote.IsAtivo)
        {
            logger.LogWarning("Vínculo {VinculoId}: pacote ausente ou inativo — assinatura não criada.", vinculo.Id);
            return ResultadoCriacaoAssinaturaAluno.PacoteIndisponivel;
        }

        var assinaturaResult = AssinaturaAluno.Criar(vinculo.Id, pacote.Id, vinculo.TreinadorId, vinculo.AlunoId, pacote.Preco, agora);
        if (assinaturaResult.IsFailure)
        {
            logger.LogWarning("Vínculo {VinculoId}: assinatura não criada ({Erro}).", vinculo.Id, assinaturaResult.Error!.Message);
            return ResultadoCriacaoAssinaturaAluno.DadosInvalidos;
        }

        if (suprimirNotificacao)
            assinaturaResult.Value.ClearDomainEvents();

        await assinaturaRepository.AdicionarAsync(assinaturaResult.Value, cancellationToken).ConfigureAwait(false);
        return ResultadoCriacaoAssinaturaAluno.Criada;
    }
}
