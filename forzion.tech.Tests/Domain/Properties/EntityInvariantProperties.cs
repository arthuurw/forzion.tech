using CsCheck;
using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Tests.Domain.Properties;

/// <summary>
/// Invariantes universais das factories <c>Criar</c> das entidades core: rejeicao de Guid
/// vazio, limites de tamanho de string (nome ≤ 100), regras monetarias (valor da assinatura
/// &gt; 0, preco do pacote ≥ 0). Determinismo via timestamp explicito.
/// </summary>
public class EntityInvariantProperties
{
    private static readonly DateTime Agora = new(2026, 5, 24, 12, 0, 0, DateTimeKind.Utc);

    // Nome valido: 1..100 chars nao-vazios (alfanumerico evita ser so espacos).
    private static readonly Gen<string> GenNomeValido = Gen.String[Gen.Char.AlphaNumeric, 1, 100];

    // Guid nao-vazio.
    private static readonly Gen<Guid> GenGuidNaoVazio = Gen.Guid.Where(g => g != Guid.Empty);

    // Geradores monetarios via centavos inteiros — evita o overflow do indexer
    // Gen.Decimal[lo, hi] da CsCheck em faixas negativas/zero (gap documentado).
    private static readonly Gen<decimal> GenValorPositivo =
        Gen.Int[1, 10_000_000].Select(cents => cents / 100m);

    private static readonly Gen<decimal> GenPrecoNaoNegativo =
        Gen.Int[0, 10_000_000].Select(cents => cents / 100m);

    private static readonly Gen<decimal> GenValorNaoPositivo =
        Gen.Int[0, 10_000_000].Select(cents => -(cents / 100m));

    private static readonly Gen<decimal> GenPrecoNegativo =
        Gen.Int[1, 10_000_000].Select(cents => -(cents / 100m));

    // --- Aluno ---

    [Fact]
    public void Aluno_Criar_ComContaIdValido_Sucede()
    {
        (from contaId in GenGuidNaoVazio
         from nome in GenNomeValido
         select (contaId, nome))
        .Sample(t =>
        {
            var aluno = Aluno.Criar(t.contaId, t.nome, Agora);
            aluno.ContaId.Should().Be(t.contaId);
            aluno.Nome.Should().Be(t.nome.Trim());
            aluno.CreatedAt.Should().Be(Agora);
        });
    }

    [Fact]
    public void Aluno_Criar_ContaIdVazio_LancaDomainException()
    {
        GenNomeValido.Sample(nome =>
        {
            var act = () => Aluno.Criar(Guid.Empty, nome, Agora);
            act.Should().Throw<DomainException>();
        });
    }

    [Fact]
    public void Aluno_Criar_NomeAcimaDe100_LancaDomainException()
    {
        (from contaId in GenGuidNaoVazio
         from nome in Gen.String[Gen.Char.AlphaNumeric, 101, 200]
         select (contaId, nome))
        .Sample(t =>
        {
            var act = () => Aluno.Criar(t.contaId, t.nome, Agora);
            act.Should().Throw<DomainException>().WithMessage("*100*");
        });
    }

    // --- Treinador ---

    [Fact]
    public void Treinador_Criar_ComContaIdValido_Sucede()
    {
        (from contaId in GenGuidNaoVazio
         from nome in GenNomeValido
         select (contaId, nome))
        .Sample(t =>
        {
            var treinador = Treinador.Criar(t.contaId, t.nome, Agora);
            treinador.ContaId.Should().Be(t.contaId);
        });
    }

    [Fact]
    public void Treinador_Criar_ContaIdVazio_LancaDomainException()
    {
        GenNomeValido.Sample(nome =>
        {
            var act = () => Treinador.Criar(Guid.Empty, nome, Agora);
            act.Should().Throw<DomainException>();
        });
    }

    // --- Pacote (preco >= 0) ---

    [Fact]
    public void Pacote_Criar_PrecoNaoNegativo_Sucede()
    {
        (from treinadorId in GenGuidNaoVazio
         from nome in GenNomeValido
         from preco in GenPrecoNaoNegativo
         select (treinadorId, nome, preco))
        .Sample(t =>
        {
            var pacote = Pacote.Criar(t.treinadorId, t.nome, t.preco, Agora);
            pacote.Preco.Should().Be(t.preco);
        });
    }

    [Fact]
    public void Pacote_Criar_PrecoNegativo_LancaDomainException()
    {
        (from treinadorId in GenGuidNaoVazio
         from nome in GenNomeValido
         from preco in GenPrecoNegativo
         select (treinadorId, nome, preco))
        .Sample(t =>
        {
            var act = () => Pacote.Criar(t.treinadorId, t.nome, t.preco, Agora);
            act.Should().Throw<DomainException>().WithMessage("*negativo*");
        });
    }

    // --- AssinaturaAluno (valor > 0) ---

    [Fact]
    public void AssinaturaAluno_Criar_ValorPositivo_Sucede()
    {
        (from vinculo in GenGuidNaoVazio
         from pacote in GenGuidNaoVazio
         from treinador in GenGuidNaoVazio
         from aluno in GenGuidNaoVazio
         from valor in GenValorPositivo
         select (vinculo, pacote, treinador, aluno, valor))
        .Sample(t =>
        {
            var assinatura = AssinaturaAluno.Criar(t.vinculo, t.pacote, t.treinador, t.aluno, t.valor, Agora);
            assinatura.Valor.Should().Be(t.valor);
        });
    }

    [Fact]
    public void AssinaturaAluno_Criar_ValorNaoPositivo_LancaDomainException()
    {
        (from vinculo in GenGuidNaoVazio
         from pacote in GenGuidNaoVazio
         from treinador in GenGuidNaoVazio
         from aluno in GenGuidNaoVazio
         from valor in GenValorNaoPositivo
         select (vinculo, pacote, treinador, aluno, valor))
        .Sample(t =>
        {
            var act = () => AssinaturaAluno.Criar(t.vinculo, t.pacote, t.treinador, t.aluno, t.valor, Agora);
            act.Should().Throw<DomainException>().WithMessage("*maior que zero*");
        });
    }

    [Fact]
    public void AssinaturaAluno_Criar_QualquerGuidVazio_LancaDomainException()
    {
        // Exatamente um dos quatro ids e vazio — deve sempre rejeitar.
        (from posicaoVazia in Gen.Int[0, 3]
         from ids in GenGuidNaoVazio.Array[4]
         from valor in GenValorPositivo
         select (posicaoVazia, ids, valor))
        .Sample(t =>
        {
            var ids = (Guid[])t.ids.Clone();
            ids[t.posicaoVazia] = Guid.Empty;

            var act = () => AssinaturaAluno.Criar(ids[0], ids[1], ids[2], ids[3], t.valor, Agora);
            act.Should().Throw<DomainException>();
        });
    }
}
