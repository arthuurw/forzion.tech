using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using System.ClientModel;

namespace forzion.tech.AI.Clients;

public sealed class ChatClientFactory : IChatClientFactory
{
    private readonly IConfiguration _config;

    public ChatClientFactory(IConfiguration config) => _config = config;

    public IChatClient CreateInternalClient()
    {
        var apiKey = _config["AI:Internal:ApiKey"]
            ?? throw new InvalidOperationException("AI:Internal:ApiKey não configurado. Use User Secrets ou variável de ambiente.");

        var model = _config["AI:Internal:Model"]
            ?? throw new InvalidOperationException("AI:Internal:Model não configurado.");

        var endpointRaw = _config["AI:Internal:Endpoint"];
        var clientOptions = endpointRaw is not null
            ? new OpenAIClientOptions { Endpoint = new Uri(endpointRaw) }
            : new OpenAIClientOptions();

        return new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions)
            .GetChatClient(model)
            .AsIChatClient();
    }
}
