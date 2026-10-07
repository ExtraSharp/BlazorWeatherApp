namespace Server;

public sealed class WeatherDashboardService(
    ApiService apiService,
    WeatherService weatherService,
    ILogger<WeatherDashboardService> logger)
{
    private readonly ApiService _apiService = apiService;
    private readonly WeatherService _weatherService = weatherService;
    private readonly ILogger<WeatherDashboardService> _logger = logger;

    public async Task<IReadOnlyList<WeatherStationOption>> GetStationOptionsAsync(CancellationToken cancellationToken = default)
    {
        var apiResponse = await _apiService.GetAllWeatherStations(cancellationToken);

        if (apiResponse?.Sources is not { Length: > 0 })
        {
            return [];
        }

        return apiResponse.Sources
            .Where(weatherStation => !string.IsNullOrWhiteSpace(weatherStation.StationId))
            .Where(weatherStation => string.Equals(weatherStation.ObservationType, "synop", StringComparison.OrdinalIgnoreCase))
            .Select(weatherStation => new WeatherStationOption
            {
                Id = weatherStation.StationId!,
                Text = NormalizeStationName(weatherStation.StationName),
                Latitude = weatherStation.Latitude,
                Longitude = weatherStation.Longitude
            })
            .DistinctBy(weatherStation => weatherStation.Id)
            .OrderBy(weatherStation => weatherStation.Text, StringComparer.CurrentCulture)
            .ToList();
    }

    public async Task<WeatherDashboardSnapshot?> GetDashboardAsync(
        string latitude,
        string longitude,
        DateTime selectedMonth,
        CancellationToken cancellationToken = default)
    {
        var currentWeather = await _apiService.GetCurrentWeatherData(latitude, longitude, cancellationToken);

        if (currentWeather?.Weather is null)
        {
            return null;
        }

        var historicTask = _weatherService.GetWeatherDataForDisplay(latitude, longitude, cancellationToken);
        var chartTask = _weatherService.GetChartDataForDisplay(latitude, longitude, selectedMonth, cancellationToken);
        var climateTask = _weatherService.GetClimateChartData(latitude, longitude, cancellationToken);

        await Task.WhenAll(historicTask, chartTask, climateTask);

        var historicalAverages = CreateHistoricalAverages(await historicTask);
        var chartData = CreateChartData(await chartTask);
        var climateRows = await climateTask;

        return new WeatherDashboardSnapshot
        {
            CurrentWeather = currentWeather,
            HistoricalAverages = historicalAverages,
            Temperatures = chartData,
            HeatMapData = climateRows.Count > 0 ? CreateHeatMapData(climateRows) : null,
            StationName = currentWeather.Sources.FirstOrDefault()?.StationName?.Trim() ?? "Unknown station",
            LastUpdated = currentWeather.Weather.TimeStamp,
            Temperature = Round(currentWeather.Weather.Temperature),
            DewPoint = Round(currentWeather.Weather.DewPoint),
            Humidity = Round(currentWeather.Weather.Humidity),
            CloudCover = Round(currentWeather.Weather.CloudCover),
            Icon = MapIcon(currentWeather.Weather.Icon)
        };
    }

    public async Task<IReadOnlyList<ChartDataModel>> GetMonthlyChartAsync(
        string latitude,
        string longitude,
        DateTime selectedMonth,
        CancellationToken cancellationToken = default)
    {
        var weatherData = await _weatherService.GetChartDataForDisplay(latitude, longitude, selectedMonth, cancellationToken);
        return CreateChartData(weatherData);
    }

    internal static WeatherDataModel CreateHistoricalAverages(IReadOnlyCollection<WeatherDataModel> days)
    {
        if (days.Count == 0)
        {
            return new WeatherDataModel();
        }

        var recordHighData = days.MaxBy(x => x.MaxTemp);
        var recordLowData = days.MinBy(x => x.MinTemp);

        return new WeatherDataModel
        {
            MeanTemp = days.Average(x => x.MeanTemp),
            MaxTemp = days.Average(x => x.MaxTemp),
            MinTemp = days.Average(x => x.MinTemp),
            Precipitation = days.Average(x => x.Precipitation),
            SunshineHours = days.Average(x => x.SunshineHours),
            RecordHigh = recordHighData?.MaxTemp ?? 0,
            RecordHighYear = recordHighData?.Year ?? 0,
            RecordLow = recordLowData?.MinTemp ?? 0,
            RecordLowYear = recordLowData?.Year ?? 0
        };
    }

    internal static IReadOnlyList<ChartDataModel> CreateChartData(IReadOnlyCollection<WeatherDataModel> groupedDayModels) =>
        groupedDayModels
            .Select(day => new ChartDataModel
            {
                X = day.Day.ToString(CultureInfo.InvariantCulture),
                High = day.MaxTemp,
                Low = day.MinTemp,
                Mean = day.MeanTemp,
                Precipitation = day.Precipitation
            })
            .ToList();

    internal static double[,] CreateHeatMapData(IReadOnlyList<WeatherDataModel> weatherData)
    {
        var matrix = new double[13, 9];

        for (var i = 0; i < 12 && i < weatherData.Count; i++)
        {
            matrix[i, 0] = Math.Round(weatherData[i].RecordLow, 1);
            matrix[i, 1] = Math.Round(weatherData[i].Precipitation, 1);
            matrix[i, 2] = Math.Round(weatherData[i].SunshineHours, 1);
            matrix[i, 3] = Math.Round(weatherData[i].MonthlyLow, 1);
            matrix[i, 4] = Math.Round(weatherData[i].MinTemp, 1);
            matrix[i, 5] = Math.Round(weatherData[i].MeanTemp, 1);
            matrix[i, 6] = Math.Round(weatherData[i].MaxTemp, 1);
            matrix[i, 7] = Math.Round(weatherData[i].MonthlyHigh, 1);
            matrix[i, 8] = Math.Round(weatherData[i].RecordHigh, 1);
        }

        matrix[12, 0] = Math.Round(weatherData.Min(data => data.RecordLow), 1);
        matrix[12, 1] = Math.Round(weatherData.Sum(data => data.Precipitation), 1);
        matrix[12, 2] = Math.Round(weatherData.Sum(data => data.SunshineHours), 1);
        matrix[12, 3] = Math.Round(weatherData.Average(data => data.MonthlyLow), 1);
        matrix[12, 4] = Math.Round(weatherData.Average(data => data.MinTemp), 1);
        matrix[12, 5] = Math.Round(weatherData.Average(data => data.MeanTemp), 1);
        matrix[12, 6] = Math.Round(weatherData.Average(data => data.MaxTemp), 1);
        matrix[12, 7] = Math.Round(weatherData.Average(data => data.MonthlyHigh), 1);
        matrix[12, 8] = Math.Round(weatherData.Max(data => data.RecordHigh), 1);

        return matrix;
    }

    internal static string? MapIcon(string? icon) =>
        icon switch
        {
            "clear-day" => "sunny",
            "cloudy" => "cloudy",
            "rainy" => "rainy",
            "partly-cloudy-day" => "partly-cloudy",
            "thunderstorm" => "thunderstorm",
            "clear-night" => "clear-night",
            "partly-cloudy-night" => "cloudy-night",
            _ => null
        };

    internal static string NormalizeStationName(string? stationName)
    {
        if (string.IsNullOrWhiteSpace(stationName))
        {
            return "Unknown station";
        }

        var textInfo = CultureInfo.CurrentCulture.TextInfo;
        var normalized = textInfo.ToTitleCase(stationName.Trim().ToLowerInvariant());

        if (!normalized.Contains('-', StringComparison.Ordinal))
        {
            return normalized;
        }

        var parts = normalized
            .Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(part => textInfo.ToTitleCase(part.ToLowerInvariant()));

        return string.Join("-", parts);
    }

    private static double? Round(float? value) =>
        value.HasValue ? Math.Round(value.Value, 1) : null;
}
