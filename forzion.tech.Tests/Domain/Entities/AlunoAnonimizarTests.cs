using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Domain.Entities;

public class AlunoAnonimizarTests
{
    private static readonly Guid ContaId = Guid.NewGuid();

    private static Aluno CriarAlunoCompleto()
        => Aluno.Criar(
            ContaId,
            "João da Silva",
            TestData.Agora,
            email: "joao@email.com",
            telefone: "11999999999",
            diasDisponiveis: 4,
            tempoDisponivelMinutos: TempoDisponivel.UmaHora,
            finalidade: FinalidadeTreino.Emagrecimento,
            focoTreino: "Cardio",
            nivelCondicionamento: NivelCondicionamento.Intermediario,
            limitacoesFisicas: "Joelho direito",
            doencas: "Nenhuma",
            observacoesAdicionais: "Prefere treinos matinais").Value;

    [Fact]
    public void Anonimizar_PrimeiraVez_RetornaSuccess()
    {
        var aluno = CriarAlunoCompleto();

        var resultado = aluno.Anonimizar(TestData.Agora);

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Anonimizar_PrimeiraVez_ScrubaTodasAsCamposPII()
    {
        var aluno = CriarAlunoCompleto();

        aluno.Anonimizar(TestData.Agora);

        aluno.Nome.Should().Be("Usuário anonimizado");
        aluno.Email.Should().BeNull();
        aluno.Telefone.Should().BeNull();
    }

    [Fact]
    public void Anonimizar_PrimeiraVez_ScrubaTodasAsCamposAnamnese()
    {
        var aluno = CriarAlunoCompleto();

        aluno.Anonimizar(TestData.Agora);

        aluno.Finalidade.Should().BeNull();
        aluno.FocoTreino.Should().BeNull();
        aluno.NivelCondicionamento.Should().BeNull();
        aluno.LimitacoesFisicas.Should().BeNull();
        aluno.Doencas.Should().BeNull();
        aluno.ObservacoesAdicionais.Should().BeNull();
        aluno.DiasDisponiveis.Should().BeNull();
        aluno.TempoDisponivelMinutos.Should().BeNull();
    }

    [Fact]
    public void Anonimizar_PrimeiraVez_PreencheUpdatedAt()
    {
        var agora = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var aluno = CriarAlunoCompleto();

        aluno.Anonimizar(agora);

        aluno.UpdatedAt.Should().Be(agora);
    }

    [Fact]
    public void Anonimizar_SegundaChamada_Idempotente()
    {
        var agora = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var aluno = CriarAlunoCompleto();
        aluno.Anonimizar(agora);

        var resultado = aluno.Anonimizar(agora.AddHours(1));

        resultado.IsSuccess.Should().BeTrue();
        aluno.Nome.Should().Be("Usuário anonimizado");
        // UpdatedAt should remain from the first call (idempotent — no re-scrub)
        aluno.UpdatedAt.Should().Be(agora);
    }

    [Fact]
    public void Anonimizar_NomeIgualAoPlaceholderMasNaoAnonimizado_AindaScrubaPII()
    {
        var aluno = Aluno.Criar(
            ContaId,
            "Usuário anonimizado",
            TestData.Agora,
            email: "real@email.com",
            telefone: "11999999999",
            finalidade: FinalidadeTreino.Emagrecimento,
            focoTreino: "Cardio",
            doencas: "Hipertensão").Value;

        var resultado = aluno.Anonimizar(TestData.Agora);

        resultado.IsSuccess.Should().BeTrue();
        aluno.Email.Should().BeNull();
        aluno.Telefone.Should().BeNull();
        aluno.Finalidade.Should().BeNull();
        aluno.FocoTreino.Should().BeNull();
        aluno.Doencas.Should().BeNull();
    }

    [Fact]
    public void Anonimizar_NomeIgualAoPlaceholder_SegundaChamadaIdempotente()
    {
        var agora = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var aluno = Aluno.Criar(
            ContaId, "Usuário anonimizado", TestData.Agora, telefone: "11999999999").Value;
        aluno.Anonimizar(agora);

        var resultado = aluno.Anonimizar(agora.AddHours(1));

        resultado.IsSuccess.Should().BeTrue();
        aluno.Telefone.Should().BeNull();
        aluno.UpdatedAt.Should().Be(agora);
    }

    [Fact]
    public void Anonimizar_AlunoSemCamposOpcionais_ScrubaApenasNome()
    {
        var aluno = Aluno.Criar(ContaId, "Carlos", TestData.Agora).Value;

        var resultado = aluno.Anonimizar(TestData.Agora);

        resultado.IsSuccess.Should().BeTrue();
        aluno.Nome.Should().Be("Usuário anonimizado");
        aluno.Email.Should().BeNull();
        aluno.Telefone.Should().BeNull();
    }
}
