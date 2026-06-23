using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Application.Outbox;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Outbox.Handlers;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Outbox;

public class EmitirNfseEfeitoHandlerTests
{
    private static readonly DateTimeOffset Instante = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTime Agora = Instante.UtcDateTime;

    private readonly Mock<INotaFiscalRepository> _notaRepo = new();
    private readonly Mock<ITreinadorRepository> _treinadorRepo = new();
    private readonly Mock<IEmissorNfseService> _emissor = new();
    private readonly Mock<IOutboxEnfileirador> _enfileirador = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _time = new(Instante);
    private readonly EmitirNfseEfeitoHandler _handler;

    public EmitirNfseEfeitoHandlerTests()
    {
        _handler = new EmitirNfseEfeitoHandler(
            _notaRepo.Object,
            _treinadorRepo.Object,
            _emissor.Object,
            _enfileirador.Object,
            Options.Create(Settings()),
            _unitOfWork.Object,
            _time,
            Mock.Of<ILogger<EmitirNfseEfeitoHandler>>());
    }

    [Fact]
    public async Task Sucesso_MarcaEmitidaEMontaDpsDoPrestadorETomador()
    {
        var (nota, treinador) = CriarCenario(comDadosFiscais: true);
        DpsInput? capturado = null;
        _emissor.Setup(e => e.EmitirAsync(It.IsAny<DpsInput>(), It.IsAny<CancellationToken>()))
            .Callback<DpsInput, CancellationToken>((i, _) => capturado = i)
            .ReturnsAsync(new NfseResultado(true, "CHV-1", "10", Agora, "danfse-ref", null, null));

        await _handler.ExecutarAsync(Payload(nota.Id));

        capturado.Should().NotBeNull();
        capturado!.Prestador.Cnpj.Should().Be("11444777000161");
        capturado.CodigoServico.Should().Be("0500");
        capturado.Aliquota.Should().Be(2m);
        capturado.Valor.Should().Be(nota.Valor);
        capturado.Tomador.Should().BeSameAs(treinador.DadosFiscais);
        capturado.NumeroDpsEstavel.Should().Be(nota.NumeroDps);

        nota.Status.Should().Be(NotaFiscalStatus.Emitida);
        nota.ChaveAcesso.Should().Be("CHV-1");
        nota.NumeroNfse.Should().Be("10");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Rejeicao_MarcaErroSemExcecao()
    {
        var (nota, _) = CriarCenario(comDadosFiscais: true);
        _emissor.Setup(e => e.EmitirAsync(It.IsAny<DpsInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NfseResultado(false, null, null, null, null, "E01", "CNPJ inválido"));

        await _handler.ExecutarAsync(Payload(nota.Id));

        nota.Status.Should().Be(NotaFiscalStatus.Erro);
        nota.CodigoErro.Should().Be("E01");
        nota.MotivoErro.Should().Be("CNPJ inválido");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Excecao_PropagaParaRetryESemCommit()
    {
        var (nota, _) = CriarCenario(comDadosFiscais: true);
        _emissor.Setup(e => e.EmitirAsync(It.IsAny<DpsInput>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("5xx"));

        var act = () => _handler.ExecutarAsync(Payload(nota.Id));

        await act.Should().ThrowAsync<HttpRequestException>();
        nota.Status.Should().Be(NotaFiscalStatus.Pendente);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SucessoSemChaveAcesso_TransicaoInvalida_LancaENaoCommita()
    {
        var (nota, _) = CriarCenario(comDadosFiscais: true);
        _emissor.Setup(e => e.EmitirAsync(It.IsAny<DpsInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NfseResultado(true, null, "10", Agora, "danfse-ref", null, null));

        var act = () => _handler.ExecutarAsync(Payload(nota.Id));

        await act.Should().ThrowAsync<InvalidOperationException>();
        nota.Status.Should().Be(NotaFiscalStatus.Pendente);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NotaJaEmitida_Ignora()
    {
        var (nota, _) = CriarCenario(comDadosFiscais: true);
        nota.MarcarEmitida("CHV-PREV", "9", Agora, null, Agora);

        await _handler.ExecutarAsync(Payload(nota.Id));

        _emissor.Verify(e => e.EmitirAsync(It.IsAny<DpsInput>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NotaInexistente_Ignora()
    {
        var id = Guid.NewGuid();
        _notaRepo.Setup(r => r.ObterPorIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((NotaFiscal?)null);

        await _handler.ExecutarAsync(Payload(id));

        _emissor.Verify(e => e.EmitirAsync(It.IsAny<DpsInput>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SemDadosFiscais_MarcaBloqueadaENaoEmite()
    {
        var (nota, _) = CriarCenario(comDadosFiscais: false);

        await _handler.ExecutarAsync(Payload(nota.Id));

        nota.Status.Should().Be(NotaFiscalStatus.BloqueadaDadosFiscais);
        _emissor.Verify(e => e.EmitirAsync(It.IsAny<DpsInput>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelamentoPendentePreEmissao_AoEmitir_SolicitaCancelamentoEEnfileira()
    {
        var (nota, _) = CriarCenario(comDadosFiscais: true);
        nota.RegistrarCancelamentoPendentePreEmissao("estorno do pagamento", Agora);
        _emissor.Setup(e => e.EmitirAsync(It.IsAny<DpsInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NfseResultado(true, "CHV-1", "10", Agora, "danfse-ref", null, null));

        await _handler.ExecutarAsync(Payload(nota.Id));

        nota.Status.Should().Be(NotaFiscalStatus.CancelamentoSolicitado);
        nota.CancelamentoPendentePreEmissao.Should().BeFalse();
        _enfileirador.Verify(e => e.Enfileirar(
            "fx:cancelar_nfse", It.IsAny<CancelarNfsePayload>(), $"fx:cancelar_nfse:{nota.Id}"), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task NotaJaEmitidaComCancelamentoPendente_ConcluiCancelamentoSemReemitir()
    {
        var (nota, _) = CriarCenario(comDadosFiscais: true);
        nota.RegistrarCancelamentoPendentePreEmissao("estorno do pagamento", Agora);
        nota.MarcarEmitida("CHV-1", "10", Agora, null, Agora);

        await _handler.ExecutarAsync(Payload(nota.Id));

        _emissor.Verify(e => e.EmitirAsync(It.IsAny<DpsInput>(), It.IsAny<CancellationToken>()), Times.Never);
        nota.Status.Should().Be(NotaFiscalStatus.CancelamentoSolicitado);
        _enfileirador.Verify(e => e.Enfileirar(
            "fx:cancelar_nfse", It.IsAny<CancelarNfsePayload>(), $"fx:cancelar_nfse:{nota.Id}"), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SemCancelamentoPendente_AoEmitir_NaoEnfileiraCancelamento()
    {
        var (nota, _) = CriarCenario(comDadosFiscais: true);
        _emissor.Setup(e => e.EmitirAsync(It.IsAny<DpsInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NfseResultado(true, "CHV-1", "10", Agora, "danfse-ref", null, null));

        await _handler.ExecutarAsync(Payload(nota.Id));

        _enfileirador.Verify(e => e.Enfileirar(
            It.IsAny<string>(), It.IsAny<CancelarNfsePayload>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RetryPosAutorizacao_TransmiteMesmoNumeroDps_PermitindoDedupGov()
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Treinador X", Agora).Value;
        var endereco = EnderecoFiscal.Criar("Rua A", "100", "Centro", "3550308", "SP", "01001000").Value;
        var dados = DadosFiscais.Criar(TipoDocumentoFiscal.Cnpj, "11222333000181", "Treinador X LTDA", endereco).Value;
        treinador.DefinirDadosFiscais(dados, Agora);
        var pagamentoId = Guid.NewGuid();
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _notaRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => NotaFiscal.CriarAssinatura(treinador.Id, pagamentoId, 99.90m, Agora).Value);
        var nDpsEnviados = new List<string>();
        _emissor.Setup(e => e.EmitirAsync(It.IsAny<DpsInput>(), It.IsAny<CancellationToken>()))
            .Callback<DpsInput, CancellationToken>((i, _) => nDpsEnviados.Add(i.NumeroDpsEstavel))
            .ReturnsAsync(new NfseResultado(true, "CHV-1", "10", Agora, "danfse-ref", null, null));

        await _handler.ExecutarAsync(Payload(Guid.NewGuid()));
        await _handler.ExecutarAsync(Payload(Guid.NewGuid()));

        nDpsEnviados.Should().HaveCount(2);
        nDpsEnviados.Distinct().Should().ContainSingle().Which.Should().Be($"AS-{pagamentoId}");
    }

    private (NotaFiscal nota, Treinador treinador) CriarCenario(bool comDadosFiscais)
    {
        var treinador = Treinador.Criar(Guid.NewGuid(), "Treinador X", Agora).Value;
        if (comDadosFiscais)
        {
            var endereco = EnderecoFiscal.Criar("Rua A", "100", "Centro", "3550308", "SP", "01001000").Value;
            var dados = DadosFiscais.Criar(TipoDocumentoFiscal.Cnpj, "11222333000181", "Treinador X LTDA", endereco).Value;
            treinador.DefinirDadosFiscais(dados, Agora);
        }

        var nota = NotaFiscal.CriarAssinatura(treinador.Id, Guid.NewGuid(), 99.90m, Agora).Value;
        _treinadorRepo.Setup(r => r.ObterPorIdAsync(treinador.Id, It.IsAny<CancellationToken>())).ReturnsAsync(treinador);
        _notaRepo.Setup(r => r.ObterPorIdAsync(nota.Id, It.IsAny<CancellationToken>())).ReturnsAsync(nota);
        return (nota, treinador);
    }

    private static string Payload(Guid notaId) =>
        System.Text.Json.JsonSerializer.Serialize(new EmitirNfsePayload(notaId));

    private static NfseSettings Settings() => new()
    {
        Habilitado = true,
        CnpjPrestador = "11444777000161",
        InscricaoMunicipal = "54321",
        CodigoMunicipioIbge = "3550308",
        SerieDps = "1",
        CodigoServicoAssinatura = "0500",
        CodigoServicoComissao = "0600",
        AliquotaIss = 2m,
        RegimeTributario = "SimplesNacional",
    };
}
