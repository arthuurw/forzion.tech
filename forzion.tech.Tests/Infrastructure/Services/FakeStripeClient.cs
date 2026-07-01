using Moq;
using Stripe;

namespace forzion.tech.Tests.Infrastructure.Services;

internal sealed class FakeStripeClient
{
    private readonly Mock<IStripeClient> _mock = new();

    public IStripeClient Object => _mock.Object;

    public Mock<IStripeClient> Mock => _mock;

    public FakeStripeClient Returns<T>(T entity, HttpMethod? method = null, string? pathContains = null)
        where T : IStripeEntity
    {
        _mock.Setup(c => c.RequestAsync<T>(
                MethodMatch(method),
                PathMatch(pathContains),
                It.IsAny<BaseOptions>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        return this;
    }

    public FakeStripeClient Throws<T>(Exception exception, HttpMethod? method = null, string? pathContains = null)
        where T : IStripeEntity
    {
        _mock.Setup(c => c.RequestAsync<T>(
                MethodMatch(method),
                PathMatch(pathContains),
                It.IsAny<BaseOptions>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);
        return this;
    }

    private static HttpMethod MethodMatch(HttpMethod? method) =>
        method is null ? It.IsAny<HttpMethod>() : It.Is<HttpMethod>(m => m == method);

    private static string PathMatch(string? pathContains) =>
        pathContains is null
            ? It.IsAny<string>()
            : It.Is<string>(p => p.Contains(pathContains, StringComparison.Ordinal));
}
