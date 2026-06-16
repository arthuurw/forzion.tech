using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using forzion.tech.Application.Interfaces;
using forzion.tech.Application.Interfaces.Repositories;
using forzion.tech.Domain.Entities;
using forzion.tech.Domain.Enums;
using forzion.tech.Domain.ValueObjects;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Infrastructure;

public class RefreshTokenServiceTests
{
    private readonly Mock<IRefreshTokenRepository> _tokenRepo = new();
    private readonly Mock<IRefreshTokenFamilyRepository> _familyRepo = new();
    private readonly Mock<IContaRepository> _contaRepo = new();
    private readonly RefreshTokenService _service;
    private static readonly DateTime Agora = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

    public RefreshTokenServiceTests()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        _service = new RefreshTokenService(_tokenRepo.Object, _familyRepo.Object, _contaRepo.Object, config,
            new Mock<ILogger<RefreshTokenService>>().Object);
    }

    private static string Hash(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();

    private static Conta NovaConta(TipoConta tipo = TipoConta.Aluno) =>
        Conta.Criar(Email.Criar("a@test.com").Value, "hash", tipo, Agora).Value;

    [Fact]
    public async Task EmitirNovaFamilia_CriaFamiliaETokenHasheado()
    {
        var conta = NovaConta();
        RefreshTokenFamily? familiaAdicionada = null;
        RefreshToken? tokenAdicionado = null;
        _familyRepo.Setup(r => r.AdicionarAsync(It.IsAny<RefreshTokenFamily>(), It.IsAny<CancellationToken>()))
            .Callback<RefreshTokenFamily, CancellationToken>((f, _) => familiaAdicionada = f);
        _tokenRepo.Setup(r => r.AdicionarAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Callback<RefreshToken, CancellationToken>((t, _) => tokenAdicionado = t);

        var emitido = await _service.EmitirNovaFamiliaAsync(conta, Agora, "Chrome", default);

        familiaAdicionada.Should().NotBeNull();
        familiaAdicionada!.ContaId.Should().Be(conta.Id);
        tokenAdicionado.Should().NotBeNull();
        // NR-1: o raw nunca é persistido — só o SHA-256.
        tokenAdicionado!.TokenHash.Should().Be(Hash(emitido.RefreshRaw));
        tokenAdicionado.TokenHash.Should().NotBe(emitido.RefreshRaw);
        emitido.FamiliaId.Should().Be(familiaAdicionada.Id);
    }

    [Fact]
    public async Task Rotacionar_TokenValido_MarcaUsadoAtomicamenteEEmiteSucessor()
    {
        var conta = NovaConta();
        var familia = RefreshTokenFamily.Criar(conta.Id, Agora.AddDays(90), Agora).Value;
        var raw = "rawvalido";
        var token = RefreshToken.Criar(familia.Id, Hash(raw), Agora.AddDays(7), Agora).Value;
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(Hash(raw), It.IsAny<CancellationToken>())).ReturnsAsync(token);
        _familyRepo.Setup(r => r.ObterPorIdAsync(familia.Id, It.IsAny<CancellationToken>())).ReturnsAsync(familia);
        _contaRepo.Setup(r => r.ObterPorIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(conta);

        DateTime? usadoEmNoClaim = null;
        Guid? sucessorIdNoClaim = null;
        _tokenRepo.Setup(r => r.MarcarUsadoSeNaoUsadoAsync(token.Id, It.IsAny<DateTime>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, DateTime, Guid, CancellationToken>((_, usado, suc, _) => { usadoEmNoClaim = usado; sucessorIdNoClaim = suc; })
            .ReturnsAsync(1);

        RefreshToken? sucessorAdicionado = null;
        _tokenRepo.Setup(r => r.AdicionarAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Callback<RefreshToken, CancellationToken>((t, _) => sucessorAdicionado = t);

        var r = await _service.RotacionarAsync(raw, Agora.AddMinutes(30), default);

        r.Resultado.Should().Be(ResultadoRotacao.Sucesso);
        r.Conta.Should().Be(conta);
        r.RefreshRaw.Should().NotBeNullOrEmpty().And.NotBe(raw);
        // Marca é atômica no banco (não muta a entidade lida): asserta sobre os args do claim.
        usadoEmNoClaim.Should().Be(Agora.AddMinutes(30));
        sucessorAdicionado.Should().NotBeNull();
        sucessorIdNoClaim.Should().Be(sucessorAdicionado!.Id);
        _tokenRepo.Verify(r => r.AdicionarAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Rotacionar_PerdeClaimAtomico_RevogaFamiliaEReuse()
    {
        // SEC-01: token não-usado na leitura, mas o claim afeta 0 linhas (concorrente venceu) ⇒ reuse.
        var conta = NovaConta();
        var familia = RefreshTokenFamily.Criar(conta.Id, Agora.AddDays(90), Agora).Value;
        var raw = "rawcorrida";
        var token = RefreshToken.Criar(familia.Id, Hash(raw), Agora.AddDays(7), Agora).Value;
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(Hash(raw), It.IsAny<CancellationToken>())).ReturnsAsync(token);
        _familyRepo.Setup(r => r.ObterPorIdAsync(familia.Id, It.IsAny<CancellationToken>())).ReturnsAsync(familia);
        _contaRepo.Setup(r => r.ObterPorIdAsync(conta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(conta);
        _tokenRepo.Setup(r => r.MarcarUsadoSeNaoUsadoAsync(token.Id, It.IsAny<DateTime>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var r = await _service.RotacionarAsync(raw, Agora.AddMinutes(5), default);

        r.Resultado.Should().Be(ResultadoRotacao.ReuseDetectado);
        familia.RevogadaEm.Should().NotBeNull();
        familia.MotivoRevogacao.Should().Be(MotivoRevogacaoFamilia.ReuseDetectado);
        _tokenRepo.Verify(r => r.AdicionarAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Rotacionar_TokenJaUsado_RevogaFamiliaEReuse()
    {
        var conta = NovaConta();
        var familia = RefreshTokenFamily.Criar(conta.Id, Agora.AddDays(90), Agora).Value;
        var raw = "rawroubado";
        var token = RefreshToken.Criar(familia.Id, Hash(raw), Agora.AddDays(7), Agora).Value;
        token.MarcarUsado(Agora.AddMinutes(1), Guid.NewGuid()); // já rotacionado antes
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(Hash(raw), It.IsAny<CancellationToken>())).ReturnsAsync(token);
        _familyRepo.Setup(r => r.ObterPorIdAsync(familia.Id, It.IsAny<CancellationToken>())).ReturnsAsync(familia);

        var r = await _service.RotacionarAsync(raw, Agora.AddMinutes(5), default);

        r.Resultado.Should().Be(ResultadoRotacao.ReuseDetectado);
        familia.RevogadaEm.Should().NotBeNull();
        familia.MotivoRevogacao.Should().Be(MotivoRevogacaoFamilia.ReuseDetectado);
        // Não emite sucessor num reuse.
        _tokenRepo.Verify(r => r.AdicionarAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Rotacionar_TokenDesconhecido_Invalido()
    {
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((RefreshToken?)null);

        var r = await _service.RotacionarAsync("qualquer", Agora, default);

        r.Resultado.Should().Be(ResultadoRotacao.Invalido);
    }

    [Fact]
    public async Task Rotacionar_FamiliaRevogada_Invalido()
    {
        var conta = NovaConta();
        var familia = RefreshTokenFamily.Criar(conta.Id, Agora.AddDays(90), Agora).Value;
        familia.Revogar(MotivoRevogacaoFamilia.Logout, Agora);
        var raw = "raw";
        var token = RefreshToken.Criar(familia.Id, Hash(raw), Agora.AddDays(7), Agora).Value;
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(Hash(raw), It.IsAny<CancellationToken>())).ReturnsAsync(token);
        _familyRepo.Setup(r => r.ObterPorIdAsync(familia.Id, It.IsAny<CancellationToken>())).ReturnsAsync(familia);

        var r = await _service.RotacionarAsync(raw, Agora.AddMinutes(5), default);

        r.Resultado.Should().Be(ResultadoRotacao.Invalido);
    }

    [Fact]
    public async Task Rotacionar_IdleExpirado_Invalido()
    {
        var conta = NovaConta();
        var familia = RefreshTokenFamily.Criar(conta.Id, Agora.AddDays(90), Agora).Value;
        var raw = "raw";
        var token = RefreshToken.Criar(familia.Id, Hash(raw), Agora.AddDays(7), Agora).Value;
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(Hash(raw), It.IsAny<CancellationToken>())).ReturnsAsync(token);
        _familyRepo.Setup(r => r.ObterPorIdAsync(familia.Id, It.IsAny<CancellationToken>())).ReturnsAsync(familia);

        var r = await _service.RotacionarAsync(raw, Agora.AddDays(8), default);

        r.Resultado.Should().Be(ResultadoRotacao.Invalido);
    }

    [Fact]
    public async Task Rotacionar_AlemAbsoluto_Invalido()
    {
        var conta = NovaConta();
        var familia = RefreshTokenFamily.Criar(conta.Id, Agora.AddDays(2), Agora).Value; // absoluto curto
        var raw = "raw";
        var token = RefreshToken.Criar(familia.Id, Hash(raw), Agora.AddDays(7), Agora).Value;
        _tokenRepo.Setup(r => r.BuscarPorHashAsync(Hash(raw), It.IsAny<CancellationToken>())).ReturnsAsync(token);
        _familyRepo.Setup(r => r.ObterPorIdAsync(familia.Id, It.IsAny<CancellationToken>())).ReturnsAsync(familia);

        // Token idle ainda válido (7d), mas família passou do teto absoluto (2d).
        var r = await _service.RotacionarAsync(raw, Agora.AddDays(3), default);

        r.Resultado.Should().Be(ResultadoRotacao.Invalido);
    }

    [Fact]
    public async Task RevogarTodasPorConta_RevogaCadaFamiliaAtiva()
    {
        var contaId = Guid.NewGuid();
        var f1 = RefreshTokenFamily.Criar(contaId, Agora.AddDays(90), Agora).Value;
        var f2 = RefreshTokenFamily.Criar(contaId, Agora.AddDays(90), Agora).Value;
        _familyRepo.Setup(r => r.ListarAtivasPorContaAsync(contaId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { f1, f2 });

        await _service.RevogarTodasPorContaAsync(contaId, MotivoRevogacaoFamilia.TrocaSenha, Agora, default);

        f1.MotivoRevogacao.Should().Be(MotivoRevogacaoFamilia.TrocaSenha);
        f2.MotivoRevogacao.Should().Be(MotivoRevogacaoFamilia.TrocaSenha);
    }
}
