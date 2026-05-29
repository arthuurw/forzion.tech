using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;
using forzion.tech.Tests.Builders;

namespace forzion.tech.Tests.Domain.Entities;

public class LogAprovacaoTests
{
    private static readonly Guid RealizadoPorId = Guid.NewGuid();
    private static readonly Guid EntidadeId = Guid.NewGuid();

    [Fact]
    public void Registrar_DadosValidos_RetornaLog()
    {
        var log = LogAprovacao.Registrar(
            TipoAcaoAprovacao.AprovacaoTreinador, RealizadoPorId, EntidadeId, "Treinador", TestData.Agora).Value;

        log.Id.Should().NotBeEmpty();
        log.TipoAcao.Should().Be(TipoAcaoAprovacao.AprovacaoTreinador);
        log.RealizadoPorId.Should().Be(RealizadoPorId);
        log.EntidadeId.Should().Be(EntidadeId);
        log.EntidadeTipo.Should().Be("Treinador");
        log.Observacao.Should().BeNull();
        log.CreatedAt.Should().BeCloseTo(TestData.Agora, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Registrar_ComObservacao_Preenche()
    {
        var log = LogAprovacao.Registrar(
            TipoAcaoAprovacao.InativacaoTreinador, RealizadoPorId, EntidadeId, "Treinador", TestData.Agora, "obs").Value;

        log.Observacao.Should().Be("obs");
    }

    [Fact]
    public void Registrar_RealizadoPorIdVazio_LancaDomainException()
    {
        var r = LogAprovacao.Registrar(TipoAcaoAprovacao.AprovacaoTreinador, Guid.Empty, EntidadeId, "Treinador", TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O identificador de quem realizou a ação é inválido.");
    }

    [Fact]
    public void Registrar_EntidadeIdVazio_LancaDomainException()
    {
        var r = LogAprovacao.Registrar(TipoAcaoAprovacao.AprovacaoTreinador, RealizadoPorId, Guid.Empty, "Treinador", TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O identificador da entidade é inválido.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Registrar_EntidadeTipoVazio_LancaDomainException(string tipo)
    {
        var r = LogAprovacao.Registrar(TipoAcaoAprovacao.AprovacaoTreinador, RealizadoPorId, EntidadeId, tipo, TestData.Agora);
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("O tipo da entidade é obrigatório.");
    }

    [Fact]
    public void Registrar_ObservacaoMuitoLonga_LancaDomainException()
    {
        var r = LogAprovacao.Registrar(TipoAcaoAprovacao.AprovacaoTreinador, RealizadoPorId, EntidadeId, "T", TestData.Agora, new string('x', 501));
        r.IsFailure.Should().BeTrue();
        r.Error!.Message.Should().Be("A observação deve ter no máximo 500 caracteres.");
    }
}
