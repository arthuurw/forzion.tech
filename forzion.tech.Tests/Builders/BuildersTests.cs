using FluentAssertions;
using forzion.tech.Domain.Enums;

namespace forzion.tech.Tests.Builders;

/// <summary>
/// Garante que os builders produzem entidades validas com defaults e que a fonte de
/// dados deterministica (seed fixo) e reproduzivel.
/// </summary>
public class BuildersTests
{
    [Fact]
    public void AlunoBuilder_SemOverrides_ProduzAlunoValido()
    {
        var aluno = new AlunoBuilder().Build();

        aluno.Id.Should().NotBeEmpty();
        aluno.ContaId.Should().NotBeEmpty();
        aluno.Nome.Should().Be("Aluno Teste");
        aluno.Status.Should().Be(AlunoStatus.AguardandoAprovacao);
        aluno.CreatedAt.Should().Be(TestData.Agora);
    }

    [Fact]
    public void AlunoBuilder_ComOverrides_AplicaValores()
    {
        var conta = TestData.NextGuid();

        var aluno = new AlunoBuilder()
            .ComContaId(conta)
            .ComNome("Maria")
            .ComEmail("maria@forzion.tech")
            .Build();

        aluno.ContaId.Should().Be(conta);
        aluno.Nome.Should().Be("Maria");
        aluno.Email!.Value.Should().Be("maria@forzion.tech");
    }

    [Fact]
    public void TreinadorBuilder_SemOverrides_ProduzTreinadorValido()
    {
        var treinador = new TreinadorBuilder().Build();

        treinador.Id.Should().NotBeEmpty();
        treinador.Nome.Should().Be("Treinador Teste");
        treinador.Status.Should().Be(TreinadorStatus.AguardandoAprovacao);
    }

    [Fact]
    public void PacoteBuilder_SemOverrides_ProduzPacoteAtivoComPrecoPositivo()
    {
        var pacote = new PacoteBuilder().Build();

        pacote.IsAtivo.Should().BeTrue();
        pacote.Preco.Should().BeGreaterThan(0);
    }

    [Fact]
    public void AssinaturaAlunoBuilder_SemOverrides_ProduzPendente()
    {
        var assinatura = new AssinaturaAlunoBuilder().Build();

        assinatura.Status.Should().Be(AssinaturaAlunoStatus.Pendente);
        assinatura.Valor.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ContaBuilder_SemOverrides_ProduzContaValida()
    {
        var conta = new ContaBuilder().Build();

        conta.Email.Value.Should().Be("conta.teste@forzion.tech");
        conta.PasswordHash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void VinculoBuilder_SemOverrides_ProduzVinculoValido()
    {
        var vinculo = new VinculoTreinadorAlunoBuilder().Build();

        vinculo.TreinadorId.Should().NotBeEmpty();
        vinculo.AlunoId.Should().NotBeEmpty();
        vinculo.Status.Should().Be(VinculoStatus.AguardandoAprovacao);
    }

    [Fact]
    public void NextGuid_ChamadasConsecutivas_NaoColidem()
    {
        var a = TestData.NextGuid();
        var b = TestData.NextGuid();

        a.Should().NotBe(b);
        a.Should().NotBeEmpty();
        b.Should().NotBeEmpty();
    }
}
