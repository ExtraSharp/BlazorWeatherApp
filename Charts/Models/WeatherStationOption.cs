namespace Server.Models;

public sealed class WeatherStationOption
{
    public string Id { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public float Latitude { get; init; }
    public float Longitude { get; init; }
}
