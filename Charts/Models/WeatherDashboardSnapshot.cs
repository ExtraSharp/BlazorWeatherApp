namespace Server.Models;

public sealed class WeatherDashboardSnapshot
{
    public required WeatherResponseModel CurrentWeather { get; init; }
    public required WeatherDataModel HistoricalAverages { get; init; }
    public required IReadOnlyList<ChartDataModel> Temperatures { get; init; }
    public double[,]? HeatMapData { get; init; }
    public string StationName { get; init; } = string.Empty;
    public DateTime LastUpdated { get; init; }
    public double? Temperature { get; init; }
    public double? DewPoint { get; init; }
    public double? Humidity { get; init; }
    public double? CloudCover { get; init; }
    public string? Icon { get; init; }
}
