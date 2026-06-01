using FluentAssertions;
using forzion.tech.Application.UseCases.Pagamentos;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Application.Pagamentos;

public class PagamentoResponseExtensionsTests
{
    private static Pagamento PagamentoComClientSecret()
    {
        var pagamento = Pagamento.Criar(Guid.NewGuid(), 149.90m, TestData.Agora).Value;
        pagamento.DefinirDadosCartao("pi_xyz", "pi_xyz_secret_abc", TestData.Agora);
        return pagamento;
    }

    [Fact]
    public void ToResponse_AliasParaTreinador_OmiteClientSecret()
    {
        var pagamento = PagamentoComClientSecret();

        var response = PagamentoResponseExtensions.ToResponse(pagamento);

        response.PagamentoId.Should().Be(pagamento.Id);
        response.AssinaturaAlunoId.Should().Be(pagamento.AssinaturaAlunoId);
        response.Valor.Should().Be(149.90m);
        response.Status.Should().Be(PagamentoStatus.Pendente);
        response.MetodoPagamento.Should().Be(pagamento.MetodoPagamento);
        response.ClientSecret.Should().BeNull("alias ToResponse delega a ToResponseTreinador");
    }

    [Fact]
    public void ToResponseAluno_IncluiClientSecret()
    {
        var pagamento = PagamentoComClientSecret();

        var response = PagamentoResponseExtensions.ToResponseAluno(pagamento);

        response.ClientSecret.Should().Be("pi_xyz_secret_abc");
    }

    [Fact]
    public void ToResponseTreinador_OmiteClientSecret()
    {
        var pagamento = PagamentoComClientSecret();

        var response = PagamentoResponseExtensions.ToResponseTreinador(pagamento);

        response.ClientSecret.Should().BeNull();
    }
}
