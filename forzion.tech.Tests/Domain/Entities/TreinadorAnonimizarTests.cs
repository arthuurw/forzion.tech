using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Domain.Entities;

public class TreinadorAnonimizarTests
{
    private static readonly Guid ContaId = Guid.NewGuid();

    private static Treinador CriarTreinador(string telefone = "11988887777")
        => Treinador.Criar(ContaId, "Carlos Silva", TestData.Agora, telefone).Value;

    [Fact]
    public void Anonimizar_PrimeiraVez_RetornaSuccess()
    {
        var treinador = CriarTreinador();

        var resultado = treinador.Anonimizar(TestData.Agora);

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Anonimizar_PrimeiraVez_ScrubaNomelETelefone()
    {
        var treinador = CriarTreinador();

        treinador.Anonimizar(TestData.Agora);

        treinador.Nome.Should().Be("Usuário anonimizado");
        treinador.Telefone.Should().BeNull();
    }

    [Fact]
    public void Anonimizar_PrimeiraVez_PreencheUpdatedAt()
    {
        var agora = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var treinador = CriarTreinador();

        treinador.Anonimizar(agora);

        treinador.UpdatedAt.Should().Be(agora);
    }

    [Fact]
    public void Anonimizar_SegundaChamada_Idempotente()
    {
        var agora = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var treinador = CriarTreinador();
        treinador.Anonimizar(agora);

        var resultado = treinador.Anonimizar(agora.AddHours(1));

        resultado.IsSuccess.Should().BeTrue();
        treinador.Nome.Should().Be("Usuário anonimizado");
        // UpdatedAt must not be refreshed on second call
        treinador.UpdatedAt.Should().Be(agora);
    }

    [Fact]
    public void Anonimizar_TreinadorSemTelefone_NomeScrubaCorrectamente()
    {
        var treinador = Treinador.Criar(ContaId, "Carlos", TestData.Agora).Value;

        var resultado = treinador.Anonimizar(TestData.Agora);

        resultado.IsSuccess.Should().BeTrue();
        treinador.Nome.Should().Be("Usuário anonimizado");
        treinador.Telefone.Should().BeNull();
    }

    [Fact]
    public void Anonimizar_NaoAlteraOutrosCampos()
    {
        var treinador = CriarTreinador();
        var contaIdOriginal = treinador.ContaId;
        var idOriginal = treinador.Id;

        treinador.Anonimizar(TestData.Agora);

        treinador.Id.Should().Be(idOriginal);
        treinador.ContaId.Should().Be(contaIdOriginal);
    }
}
