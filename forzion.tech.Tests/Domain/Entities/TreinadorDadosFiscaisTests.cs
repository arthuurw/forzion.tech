using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;

namespace forzion.tech.Tests.Domain.Entities;

public class TreinadorDadosFiscaisTests
{
    private static readonly DateTime Agora = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Treinador NovoTreinador()
        => Treinador.Criar(Guid.NewGuid(), "Treinador Teste", Agora).Value;

    private static DadosFiscais Dados(string razao = "João da Silva")
    {
        var endereco = EnderecoFiscal.Criar("Rua das Flores", "100", "Centro", "3550308", "SP", "01001000").Value;
        return DadosFiscais.Criar(TipoDocumentoFiscal.Cpf, "11144477735", razao, endereco).Value;
    }

    [Fact]
    public void DefinirDadosFiscais_Valido_Persiste()
    {
        var treinador = NovoTreinador();
        var dados = Dados();

        var result = treinador.DefinirDadosFiscais(dados, Agora);

        result.IsSuccess.Should().BeTrue();
        treinador.DadosFiscais.Should().Be(dados);
        treinador.UpdatedAt.Should().Be(Agora);
    }

    [Fact]
    public void DefinirDadosFiscais_Atualiza_SubstituiAnterior()
    {
        var treinador = NovoTreinador();
        treinador.DefinirDadosFiscais(Dados("Antigo"), Agora);

        var novos = Dados("Novo");
        treinador.DefinirDadosFiscais(novos, Agora.AddDays(1));

        treinador.DadosFiscais!.RazaoSocial.Should().Be("Novo");
    }

    [Fact]
    public void DefinirDadosFiscais_TreinadorAnonimizado_Falha()
    {
        var treinador = NovoTreinador();
        treinador.Anonimizar(Agora);

        treinador.DefinirDadosFiscais(Dados(), Agora).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Anonimizar_PreservaDadosFiscais_GuardaFiscal()
    {
        var treinador = NovoTreinador();
        var dados = Dados();
        treinador.DefinirDadosFiscais(dados, Agora);

        treinador.Anonimizar(Agora.AddDays(1));

        treinador.DadosFiscais.Should().Be(dados);
    }
}
