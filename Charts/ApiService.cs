namespace Server;

public sealed class ApiService(
    HttpClient httpClient,
    IMemoryCache cache,
    IOptions<BrightSkyOptions> options,
    ILogger<ApiService> logger)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly IMemoryCache _cache = cache;
    private readonly BrightSkyOptions _options = options.Value;
    private readonly ILogger<ApiService> _logger = logger;

    private TimeSpan CurrentWeatherCacheDuration => TimeSpan.FromMinutes(_options.CurrentWeatherCacheMinutes);
    private TimeSpan HistoricalCacheDuration => TimeSpan.FromHours(_options.HistoricalCacheHours);
    private TimeSpan StationCacheDuration => TimeSpan.FromHours(_options.StationCacheHours);

    public Task<WeatherResponseModel?> GetCurrentWeatherData(
        string latitude,
        string longitude,
        CancellationToken cancellationToken = default) =>
        GetCachedResponseAsync<WeatherResponseModel>(
            cacheKey: $"current:{latitude}:{longitude}",
            cacheDuration: CurrentWeatherCacheDuration,
            path: "current_weather",
            query: new Dictionary<string, string?>
            {
                ["lat"] = latitude,
                ["lon"] = longitude
            },
            cancellationToken);

    public Task<MultipleWeatherResponseModel?> GetHistoricalWeatherData(
        string date,
        string latitude,
        string longitude,
        CancellationToken cancellationToken = default) =>
        GetCachedResponseAsync<MultipleWeatherResponseModel>(
            cacheKey: $"historical:{date}:{latitude}:{longitude}",
            cacheDuration: HistoricalCacheDuration,
            path: "weather",
            query: new Dictionary<string, string?>
            {
                ["lat"] = latitude,
                ["lon"] = longitude,
                ["date"] = date
            },
            cancellationToken);

    public Task<MultipleWeatherResponseModel?> GetWeatherRangeData(
        string date,
        string lastDate,
        string latitude,
        string longitude,
        CancellationToken cancellationToken = default) =>
        GetCachedResponseAsync<MultipleWeatherResponseModel>(
            cacheKey: $"range:{date}:{lastDate}:{latitude}:{longitude}",
            cacheDuration: HistoricalCacheDuration,
            path: "weather",
            query: new Dictionary<string, string?>
            {
                ["lat"] = latitude,
                ["lon"] = longitude,
                ["date"] = date,
                ["last_date"] = lastDate
            },
            cancellationToken);

    public Task<MultipleWeatherResponseModel?> GetAllWeatherStations(CancellationToken cancellationToken = default) =>
        GetCachedResponseAsync<MultipleWeatherResponseModel>(
            cacheKey: "stations:all",
            cacheDuration: StationCacheDuration,
            path: "sources",
            query: new Dictionary<string, string?>
            {
                ["lat"] = "51.7",
                ["lon"] = "10",
                ["max_dist"] = "500000"
            },
            cancellationToken);

    private async Task<T?> GetCachedResponseAsync<T>(
        string cacheKey,
        TimeSpan cacheDuration,
        string path,
        Dictionary<string, string?> query,
        CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(cacheKey, out T? cachedResponse))
        {
            _logger.LogDebug("Bright Sky cache hit for {CacheKey}", cacheKey);
            return cachedResponse;
        }

        var requestUri = QueryHelpers.AddQueryString(path, query);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogDebug("Fetching Bright Sky resource {RequestUri}", requestUri);

            var response = await _httpClient.GetFromJsonAsync<T>(requestUri, cancellationToken);

            stopwatch.Stop();

            if (response is null)
            {
                _logger.LogWarning(
                    "Bright Sky returned an empty payload for {RequestUri} after {ElapsedMs}ms",
                    requestUri,
                    stopwatch.ElapsedMilliseconds);
                return default;
            }

            _cache.Set(cacheKey, response, cacheDuration);

            _logger.LogDebug(
                "Fetched Bright Sky resource {RequestUri} in {ElapsedMs}ms",
                requestUri,
                stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            _logger.LogDebug(
                "Bright Sky request {RequestUri} was canceled after {ElapsedMs}ms",
                requestUri,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (HttpRequestException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            stopwatch.Stop();
            _logger.LogDebug(
                "Bright Sky returned no data for {RequestUri} after {ElapsedMs}ms",
                requestUri,
                stopwatch.ElapsedMilliseconds);
            return default;
        }
        catch (HttpRequestException exception)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                exception,
                "Bright Sky request failed for {RequestUri} after {ElapsedMs}ms",
                requestUri,
                stopwatch.ElapsedMilliseconds);
            return default;
        }
        catch (NotSupportedException exception)
        {
            stopwatch.Stop();
            _logger.LogError(
                exception,
                "Bright Sky payload type is not supported for {RequestUri}",
                requestUri);
            return default;
        }
        catch (System.Text.Json.JsonException exception)
        {
            stopwatch.Stop();
            _logger.LogError(
                exception,
                "Bright Sky payload could not be deserialized for {RequestUri}",
                requestUri);
            return default;
        }
    }
}
