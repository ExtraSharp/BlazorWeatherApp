using Microsoft.Extensions.Caching.Memory;
using RestSharp;

namespace Server;
public class ApiService(IMemoryCache cache)
{
    private const string BaseUrl = "https://api.brightsky.dev/";
    private readonly RestClient _client = new(BaseUrl);
    private readonly IMemoryCache _cache = cache;

    private static readonly TimeSpan CurrentWeatherCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan HistoricalCacheDuration = TimeSpan.FromHours(12);
    private static readonly TimeSpan StationCacheDuration = TimeSpan.FromHours(12);

    public async Task<WeatherResponseModel?> GetCurrentWeatherData(string latitude, string longitude)
    {
        var cacheKey = $"current:{latitude}:{longitude}";
        if (_cache.TryGetValue(cacheKey, out WeatherResponseModel? cached))
        {
            return cached;
        }

        try
        {
            var response = await _client.GetJsonAsync<WeatherResponseModel>(
                $"current_weather?lat={latitude}&lon={longitude}");

            if (response != null)
            {
                _cache.Set(cacheKey, response, CurrentWeatherCacheDuration);
            }

            return response;
        }
        catch
        {
            return null;
        }
    }

    public async Task<MultipleWeatherResponseModel?> GetHistoricalWeatherData(string date, string latitude, string longitude)
    {
        var cacheKey = $"historical:{date}:{latitude}:{longitude}";
        if (_cache.TryGetValue(cacheKey, out MultipleWeatherResponseModel? cached))
        {
            return cached;
        }

        try
        {
            var response = await _client.GetJsonAsync<MultipleWeatherResponseModel>(
                $"weather?lat={latitude}&lon={longitude}&date={date}");

            if (response != null)
            {
                _cache.Set(cacheKey, response, HistoricalCacheDuration);
            }

            return response;
        }
        catch
        {
            return null;
        }
    }

    public async Task<MultipleWeatherResponseModel?> GetMonthlyData(string date, string lastDate, string latitude, string longitude)
    {
        var cacheKey = $"monthly:{date}:{lastDate}:{latitude}:{longitude}";
        if (_cache.TryGetValue(cacheKey, out MultipleWeatherResponseModel? cached))
        {
            return cached;
        }

        try
        {
            var response = await _client.GetJsonAsync<MultipleWeatherResponseModel>(
                $"weather?lat={latitude}&lon={longitude}&date={date}&last_date={lastDate}");

            if (response != null)
            {
                _cache.Set(cacheKey, response, HistoricalCacheDuration);
            }

            return response;
        }
        catch
        {
            return null;
        }
    }

    public async Task<MultipleWeatherResponseModel?> GetEntireData(string date, string lastDate, string latitude, string longitude)
    {
        var cacheKey = $"entire:{date}:{lastDate}:{latitude}:{longitude}";
        if (_cache.TryGetValue(cacheKey, out MultipleWeatherResponseModel? cached))
        {
            return cached;
        }

        try
        {
            var response = await _client.GetJsonAsync<MultipleWeatherResponseModel>(
                $"weather?lat={latitude}&lon={longitude}&date={date}&last_date={lastDate}");

            if (response != null)
            {
                _cache.Set(cacheKey, response, HistoricalCacheDuration);
            }

            return response;
        }
        catch
        {
            return null;
        }
    }

    public async Task<MultipleWeatherResponseModel?> GetAllWeatherStations()
    {
        const string cacheKey = "stations:all";
        if (_cache.TryGetValue(cacheKey, out MultipleWeatherResponseModel? cached))
        {
            return cached;
        }

        try
        {
            var response = await _client.GetJsonAsync<MultipleWeatherResponseModel>(
                "sources?lat=51.7&lon=10&max_dist=500000");

            if (response != null)
            {
                _cache.Set(cacheKey, response, StationCacheDuration);
            }

            return response;
        }
        catch
        {
            return null;
        }
    }
}
