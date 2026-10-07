namespace Server.Tests;

using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

public sealed class ApiServiceTests
{
    [Fact]
    public async Task GetWeatherRangeData_UsesCacheForRepeatedRequests()
    {
        var handler = new CountingHandler(
            """
            {
              "weather": [
                {
                  "timestamp": "2024-01-01T00:00:00Z",
                  "temperature": 1.5
                }
              ],
              "sources": []
            }
            """);

        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.brightsky.dev/")
        };

        var service = new ApiService(
            httpClient,
            cache,
            Options.Create(new BrightSkyOptions()),
            NullLogger<ApiService>.Instance);

        var first = await service.GetWeatherRangeData("2024-01-01", "2024-01-31", "51.04", "13.74");
        var second = await service.GetWeatherRangeData("2024-01-01", "2024-01-31", "51.04", "13.74");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(1, handler.RequestCount);
    }

    private sealed class CountingHandler(string payload) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            });
        }
    }
}
