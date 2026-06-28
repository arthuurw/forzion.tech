using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;

namespace forzion.tech.Domain.Entities;

public class RedefinicaoSenhaSegundoFator
{
    public const int MaximoTentativas = 5;
    public static readonly TimeSpan Janela = TimeSpan.FromMinutes(15);

    public Guid Id { get; private set; }
    public Guid ContaId { get; private set; }
    public int Tentativas { get; private set; }
    public DateTime JanelaInicio { get; private set; }
    public DateTime AtualizadoEm { get; private set; }

    private RedefinicaoSenhaSegundoFator() { }

    public static Result<RedefinicaoSenhaSegundoFator> Criar(Guid contaId, DateTime agora)
    {
        if (contaId == Guid.Empty)
            return Result.Failure<RedefinicaoSenhaSegundoFator>(RedefinicaoSenhaSegundoFatorErrors.ContaIdInvalido);

        return Result.Success(new RedefinicaoSenhaSegundoFator
        {
            Id = Guid.NewGuid(),
            ContaId = contaId,
            Tentativas = 0,
            JanelaInicio = agora,
            AtualizadoEm = agora
        });
    }

    public bool JanelaExpirada(DateTime agora) => agora >= JanelaInicio + Janela;

    public bool Bloqueado(DateTime agora) => !JanelaExpirada(agora) && Tentativas >= MaximoTentativas;

    public Result GarantirNaoBloqueado(DateTime agora) =>
        Bloqueado(agora)
            ? Result.Failure(RedefinicaoSenhaSegundoFatorErrors.Bloqueado)
            : Result.Success();

    public void RegistrarFalha(DateTime agora)
    {
        if (JanelaExpirada(agora))
        {
            Tentativas = 0;
            JanelaInicio = agora;
        }

        Tentativas++;
        AtualizadoEm = agora;
    }

    public void RegistrarSucesso(DateTime agora)
    {
        Tentativas = 0;
        JanelaInicio = agora;
        AtualizadoEm = agora;
    }
}
