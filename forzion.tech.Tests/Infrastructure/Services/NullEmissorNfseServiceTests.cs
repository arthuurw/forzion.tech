using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Services;

public class NullEmissorNfseServiceTests
{
    private readonly NullEmissorNfseService _service = new(Mock.Of<ILogger<NullEmissorNfseService>>());

    private static DpsInput DpsValido()
    {
        var endereco = EnderecoFiscal.Criar("Rua A", "100", "Centro", "3550308", "SP", "01001000").Value;
        var tomador = DadosFiscais.Criar(TipoDocumentoFiscal.Cnpj, "11222333000181", "Treinador LTDA", endereco).Value;
        var prestador = new DpsPrestador("11222333000181", "12345", "3550308", "Simples");
        return new DpsInput(prestador, tomador, "01.05", 2m, 99.90m, new DateOnly(2026, 1, 1), "DPS-001");
    }

    [Fact]
    public async Task EmitirAsync_NaoEmiteRetornaFalha()
    {
        var resultado = await _service.EmitirAsync(DpsValido());

        resultado.Sucesso.Should().BeFalse();
        resultado.ChaveAcesso.Should().BeNull();
        resultado.CodigoErro.Should().Be("NFSE_DESABILITADO");
    }

    [Fact]
    public async Task ConsultarAsync_RetornaNaoEncontrada()
    {
        var status = await _service.ConsultarAsync("CHAVE-X");

        status.Situacao.Should().Be(NfseSituacao.NaoEncontrada);
        status.CodigoErro.Should().Be("NFSE_DESABILITADO");
    }

    [Fact]
    public async Task CancelarAsync_NaoCancelaRetornaFalha()
    {
        var resultado = await _service.CancelarAsync("CHAVE-X", "motivo");

        resultado.Sucesso.Should().BeFalse();
        resultado.CodigoErro.Should().Be("NFSE_DESABILITADO");
    }
}
