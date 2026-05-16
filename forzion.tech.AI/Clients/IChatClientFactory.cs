using Microsoft.Extensions.AI;

namespace forzion.tech.AI.Clients;

public interface IChatClientFactory
{
    IChatClient CreateInternalClient();
}
