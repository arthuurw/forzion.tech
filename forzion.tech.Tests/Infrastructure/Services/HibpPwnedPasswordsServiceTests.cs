using System.Net;
using System.Text;
using FluentAssertions;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Services;

public class HibpPwnedPasswordsServiceTests
{
    private const string Senha = "P@ssw0rd";
    private const string Prefixo = "21BD1";
    private const string Sufixo = "2DC183F740EE76F27B78EB39C8AD972A757";

    private static string CorpusCom(string sufixoContagem) =>
        "0018A45C4D1DEF81644B54AB7F969B88D65:1\r\n" +
        sufixoContagem + "\r\n" +
        "00D4F6E8FA6EECAD2A3AA415EEC418D38EC:2";

    [Fact]
    public async Task EstaComprometidaAsync_SenhaNoCorpus_RetornaTrue()
    {
        var (svc, _) = Criar(_ => Resposta(HttpStatusCode.OK, CorpusCom($"{Sufixo}:99")));

        var resultado = await svc.EstaComprometidaAsync(Senha, CancellationToken.None);

        resultado.Should().BeTrue();
    }

    [Fact]
    public async Task EstaComprometidaAsync_SenhaForaDoCorpus_RetornaFalse()
    {
        var (svc, _) = Criar(_ => Resposta(HttpStatusCode.OK, CorpusCom("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA:7")));

        var resultado = await svc.EstaComprometidaAsync(Senha, CancellationToken.None);

        resultado.Should().BeFalse();
    }

    [Fact]
    public async Task EstaComprometidaAsync_SufixoComContagemZero_RetornaFalse()
    {
        var (svc, _) = Criar(_ => Resposta(HttpStatusCode.OK, CorpusCom($"{Sufixo}:0")));

        var resultado = await svc.EstaComprometidaAsync(Senha, CancellationToken.None);

        resultado.Should().BeFalse();
    }

    [Fact]
    public async Task EstaComprometidaAsync_Status5xx_FailOpenRetornaFalse()
    {
        var (svc, _) = Criar(_ => Resposta(HttpStatusCode.ServiceUnavailable, string.Empty));

        var resultado = await svc.EstaComprometidaAsync(Senha, CancellationToken.None);

        resultado.Should().BeFalse();
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(TaskCanceledException))]
    public async Task EstaComprometidaAsync_TimeoutOuFalhaRede_FailOpenRetornaFalse(Type excecao)
    {
        var (svc, _) = Criar(_ => throw (Exception)Activator.CreateInstance(excecao)!);

        var resultado = await svc.EstaComprometidaAsync(Senha, CancellationToken.None);

        resultado.Should().BeFalse();
    }

    [Fact]
    public async Task EstaComprometidaAsync_CancelamentoDoCaller_Propaga()
    {
        var (svc, _) = Criar(_ => throw new TaskCanceledException());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var acao = async () => await svc.EstaComprometidaAsync(Senha, cts.Token);

        await acao.Should().ThrowAsync<TaskCanceledException>();
    }

    [Fact]
    public async Task EstaComprometidaAsync_EnviaApenasPrefixoDe5_KAnonymity()
    {
        var (svc, handler) = Criar(_ => Resposta(HttpStatusCode.OK, CorpusCom($"{Sufixo}:99")));

        await svc.EstaComprometidaAsync(Senha, CancellationToken.None);

        handler.UltimaUri!.AbsoluteUri.Should().Be($"https://api.pwnedpasswords.com/range/{Prefixo}");
        handler.UltimaUri.AbsoluteUri.Should().NotContain(Sufixo);
        handler.AddPadding.Should().Be("true");
    }

    private static (HibpPwnedPasswordsService svc, FakeHandler handler) Criar(
        Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new FakeHandler(responder);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.pwnedpasswords.com/") };
        var svc = new HibpPwnedPasswordsService(client, Mock.Of<ILogger<HibpPwnedPasswordsService>>());
        return (svc, handler);
    }

    private static HttpResponseMessage Resposta(HttpStatusCode status, string corpo) =>
        new(status) { Content = new StringContent(corpo, Encoding.UTF8, "text/plain") };

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public Uri? UltimaUri { get; private set; }
        public string? AddPadding { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            UltimaUri = request.RequestUri;
            AddPadding = request.Headers.TryGetValues("Add-Padding", out var valores) ? string.Join(",", valores) : null;
            return Task.FromResult(responder(request));
        }
    }
}
