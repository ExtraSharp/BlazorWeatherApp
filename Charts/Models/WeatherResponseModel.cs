namespace Server.Models;

public sealed class MultipleWeatherResponseModel
{
    [JsonPropertyName("weather")]
    public WeatherModel?[] Weather { get; init; } = [];

    [JsonPropertyName("sources")]
    public Source[] Sources { get; init; } = [];
}

public sealed class WeatherResponseModel
{
    [JsonPropertyName("weather")]
    public WeatherModel? Weather { get; init; }

    [JsonPropertyName("sources")]
    public Source[] Sources { get; init; } = [];
}

public sealed class WeatherModel
{
    [JsonPropertyName("timestamp")]
    public DateTime TimeStamp { get; init; }

    [JsonPropertyName("temperature")]
    public float? Temperature { get; init; }
    
    [JsonPropertyName("precipitation")]
    public float? Precipitation { get; init; }
    
    [JsonPropertyName("sunshine")]
    public float? SunshineHours { get; init; }

    [JsonPropertyName("icon")]
    public string? Icon { get; init; }

    [JsonPropertyName("condition")]
    public string? Condition { get; init; }

    [JsonPropertyName("dew_point")]
    public float? DewPoint { get; init; }

    [JsonPropertyName("relative_humidity")]
    public float? Humidity { get; init; }

    [JsonPropertyName("cloud_cover")]
    public float? CloudCover { get; init; }
}

public sealed class Source
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("dwd_station_id")]
    public string? StationId { get; init; }

    [JsonPropertyName("station_name")]
    public string? StationName { get; init; }

    [JsonPropertyName("distance")]
    public float? Distance { get; init; }

    [JsonPropertyName("observation_type")]
    public string? ObservationType { get; init; }

    [JsonPropertyName("lat")]
    public float Latitude { get; init; }

    [JsonPropertyName("lon")]
    public float Longitude { get; init; }
}
