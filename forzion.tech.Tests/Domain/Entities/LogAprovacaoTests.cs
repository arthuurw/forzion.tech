using FluentAssertions;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.Exceptions;

namespace forzion.tech.Tests.Domain.Entities;

public class LogAprovacaoTests
{
    private static readonly Guid RealizadoPorId = Guid.NewGuid();
    private static readonly Guid EntidadeId = Guid.NewGuid();

    [Fact]
    public void Registrar_DadosValidos_RetornaLog()
    {
        var log = LogAprovacao.Registrar(
            TipoAcaoAprovacao.AprovacaoTreinador, RealizadoPorId, EntidadeId, "Treinador", DateTime.UtcNow);

        log.Id.Should().NotBeEmpty();
        log.TipoAcao.Should().Be(TipoAcaoAprovacao.AprovacaoTreinador);
        log.RealizadoPorId.Should().Be(RealizadoPorId);
        log.EntidadeId.Should().Be(EntidadeId);
        log.EntidadeTipo.Should().Be("Treinador");
        log.Observacao.Should().BeNull();
        log.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Registrar_ComObservacao_Preenche()
    {
        var log = LogAprovacao.Registrar(
            TipoAcaoAprovacao.InativacaoTreinador, RealizadoPorId, EntidadeId, "Treinador", DateTime.UtcNow, "obs");

        log.Observacao.Should().Be("obs");
    }

    [Fact]
    public void Registrar_RealizadoPorIdVazio_LancaDomainException()
    {
        var act = () => LogAprovacao.Registrar(TipoAcaoAprovacao.AprovacaoTreinador, Guid.Empty, EntidadeId, "Treinador", DateTime.UtcNow);
        act.Should().Throw<DomainException>().WithMessage("O identificador de quem realizou a ação é inválido.");
    }

    [Fact]
    public void Registrar_EntidadeIdVazio_LancaDomainException()
    {
        var act = () => LogAprovacao.Registrar(TipoAcaoAprovacao.AprovacaoTreinador, RealizadoPorId, Guid.Empty, "Treinador", DateTime.UtcNow);
        act.Should().Throw<DomainException>().WithMessage("O identificador da entidade é inválido.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Registrar_EntidadeTipoVazio_LancaDomainException(string tipo)
    {
        var act = () => LogAprovacao.Registrar(TipoAcaoAprovacao.AprovacaoTreinador, RealizadoPorId, EntidadeId, tipo, DateTime.UtcNow);
        act.Should().Throw<DomainException>().WithMessage("O tipo da entidade é obrigatório.");
    }

    [Fact]
    public void Registrar_ObservacaoMuitoLonga_LancaDomainException()
    {
        var act = () => LogAprovacao.Registrar(TipoAcaoAprovacao.AprovacaoTreinador, RealizadoPorId, EntidadeId, "T", DateTime.UtcNow, new string('x', 501));
        act.Should().Throw<DomainException>().WithMessage("A observação deve ter no máximo 500 caracteres.");
    }
}
