using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using forzion.tech.Application.Interfaces;
using forzion.tech.Domain.Shared;
using forzion.tech.Domain.Shared.Errors;
using Microsoft.Extensions.Logging;

namespace forzion.tech.Infrastructure.Services;

public sealed class ViaCepConsultaCepService(HttpClient httpClient, ILogger<ViaCepConsultaCepService> logger) : IConsultaCepService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<ConsultaCepResultado>> ConsultarAsync(string cep, CancellationToken cancellationToken)
    {
        var digitos = Digitos.Apenas(cep);
        if (digitos.Length != 8)
            return Result.Failure<ConsultaCepResultado>(ConsultaCepErrors.CepInvalido);

        try
        {
            using var resposta = await httpClient.GetAsync($"{digitos}/json/", cancellationToken).ConfigureAwait(false);
            if (!resposta.IsSuccessStatusCode)
                return Result.Failure<ConsultaCepResultado>(ConsultaCepErrors.ServicoIndisponivel);

            var dto = await resposta.Content
                .ReadFromJsonAsync<ViaCepResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);
            if (dto is null)
                return Result.Failure<ConsultaCepResultado>(ConsultaCepErrors.ServicoIndisponivel);

            if (dto.Erro)
                return Result.Failure<ConsultaCepResultado>(ConsultaCepErrors.CepNaoEncontrado);

            return Result.Success(new ConsultaCepResultado(
                dto.Logradouro ?? string.Empty,
                dto.Complemento ?? string.Empty,
                dto.Bairro ?? string.Empty,
                dto.Localidade ?? string.Empty,
                dto.Uf ?? string.Empty,
                dto.Ibge ?? string.Empty));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Falha ao consultar CEP no ViaCEP.");
            return Result.Failure<ConsultaCepResultado>(ConsultaCepErrors.ServicoIndisponivel);
        }
    }

    private sealed record ViaCepResponse(
        string? Logradouro,
        string? Complemento,
        string? Bairro,
        string? Localidade,
        string? Uf,
        string? Ibge,
        [property: JsonConverter(typeof(BoolTolerante))] bool Erro = false);

    private sealed class BoolTolerante : JsonConverter<bool>
    {
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType switch
            {
                JsonTokenType.True => true,
                JsonTokenType.False => false,
                JsonTokenType.String => bool.TryParse(reader.GetString(), out var b) && b,
                _ => false,
            };

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) =>
            writer.WriteBooleanValue(value);
    }
}
