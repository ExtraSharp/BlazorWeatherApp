namespace Server.Options;

public sealed class BrightSkyOptions
{
    public const string SectionName = "BrightSky";

    public string BaseUrl { get; set; } = "https://api.brightsky.dev/";
    public int CurrentWeatherCacheMinutes { get; set; } = 5;
    public int HistoricalCacheHours { get; set; } = 12;
    public int StationCacheHours { get; set; } = 12;
    public int RequestTimeoutSeconds { get; set; } = 30;
}
