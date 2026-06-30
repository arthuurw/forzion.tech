using System.Net;
using System.Text;
using FluentAssertions;
using forzion.tech.Domain.Shared.Errors;
using forzion.tech.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace forzion.tech.Tests.Infrastructure.Services;

public class ViaCepConsultaCepServiceTests
{
    private const string PayloadSe =
        "{\"cep\":\"01001-000\",\"logradouro\":\"Praça da Sé\",\"complemento\":\"lado ímpar\"," +
        "\"bairro\":\"Sé\",\"localidade\":\"São Paulo\",\"uf\":\"SP\",\"ibge\":\"3550308\"}";

    [Fact]
    public async Task ConsultarAsync_CepComMenosDe8Digitos_RetornaCepInvalidoSemChamarHandler()
    {
        var (svc, handler) = Criar(_ => Resposta(HttpStatusCode.OK, PayloadSe));

        var resultado = await svc.ConsultarAsync("123", CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().Be(ConsultaCepErrors.CepInvalido);
        handler.Chamadas.Should().Be(0);
    }

    [Fact]
    public async Task ConsultarAsync_Sucesso_MapeiaIbgeECamposESanitizaUrl()
    {
        var (svc, handler) = Criar(_ => Resposta(HttpStatusCode.OK, PayloadSe));

        var resultado = await svc.ConsultarAsync("01001-000", CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.CodigoMunicipioIbge.Should().Be("3550308");
        resultado.Value.Logradouro.Should().Be("Praça da Sé");
        resultado.Value.Complemento.Should().Be("lado ímpar");
        resultado.Value.Bairro.Should().Be("Sé");
        resultado.Value.Localidade.Should().Be("São Paulo");
        resultado.Value.Uf.Should().Be("SP");
        handler.UltimaUri!.AbsoluteUri.Should().Be("https://viacep.com.br/ws/01001000/json/");
    }

    [Theory]
    [InlineData("{\"erro\":true}")]
    [InlineData("{\"erro\":\"true\"}")]
    public async Task ConsultarAsync_ErroVerdadeiro_RetornaCepNaoEncontrado(string corpo)
    {
        var (svc, _) = Criar(_ => Resposta(HttpStatusCode.OK, corpo));

        var resultado = await svc.ConsultarAsync("99999999", CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().Be(ConsultaCepErrors.CepNaoEncontrado);
    }

    [Fact]
    public async Task ConsultarAsync_StatusNaoSucesso_RetornaServicoIndisponivel()
    {
        var (svc, _) = Criar(_ => Resposta(HttpStatusCode.InternalServerError, string.Empty));

        var resultado = await svc.ConsultarAsync("01001000", CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().Be(ConsultaCepErrors.ServicoIndisponivel);
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(TaskCanceledException))]
    public async Task ConsultarAsync_FalhaDeRedeOuTimeout_RetornaServicoIndisponivelSemExcecao(Type excecao)
    {
        var (svc, _) = Criar(_ => throw (Exception)Activator.CreateInstance(excecao)!);

        var resultado = await svc.ConsultarAsync("01001000", CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().Be(ConsultaCepErrors.ServicoIndisponivel);
    }

    [Fact]
    public async Task ConsultarAsync_IbgeVazioEmCepValido_RetornaSucessoComIbgeVazio()
    {
        const string payload =
            "{\"logradouro\":\"Rua X\",\"bairro\":\"Centro\",\"localidade\":\"Cidade\",\"uf\":\"SP\",\"ibge\":\"\"}";
        var (svc, _) = Criar(_ => Resposta(HttpStatusCode.OK, payload));

        var resultado = await svc.ConsultarAsync("01001000", CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.CodigoMunicipioIbge.Should().BeEmpty();
        resultado.Value.Logradouro.Should().Be("Rua X");
    }

    private static (ViaCepConsultaCepService svc, FakeHandler handler) Criar(
        Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new FakeHandler(responder);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://viacep.com.br/ws/") };
        var svc = new ViaCepConsultaCepService(client, Mock.Of<ILogger<ViaCepConsultaCepService>>());
        return (svc, handler);
    }

    private static HttpResponseMessage Resposta(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int Chamadas { get; private set; }
        public Uri? UltimaUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Chamadas++;
            UltimaUri = request.RequestUri;
            return Task.FromResult(responder(request));
        }
    }
}
