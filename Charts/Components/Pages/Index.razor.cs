namespace Server.Components.Pages;

public partial class Index : IAsyncDisposable
{
    #region Private Members
    public class WeatherStations
    {
        public string ID { get; set; }
        public string Text { get; set; }
        public float Latitude { get; set; }
        public float Longitude { get; set; }
    }
    public object HeatMapData { get; set; }
    private string? ClimateErrorMessage { get; set; }
    private DateTime LastUpdated { get; set; }

    private string LastUpdatedTime
    {
        get
        {
            var cstTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(LastUpdated, "Central European Standard Time");
            return cstTime.ToString("HH:mm");
        }
    }

    private DotNetObjectReference<Index>? _dotNetRef;
    private string Width { get; set; } = "900";
    private List<ChartDataModel> Temperatures = [];
    private int ViewportWidth { get; set; }
    private WeatherResponseModel? CurrentWeather { get; set; }
    private string? StationName { get; set; }
    private string? ErrorMessage { get; set; }
    private WeatherDataModel HistoricalAverages { get; set; } = new();
    private double? CloudCover { get; set; }
    private string Latitude { get; set; }
    private List<WeatherStations> StationData = new();
    private string Longitude { get; set; }
    private string? Icon { get; set; }
    private double? _temperature;
    private string DataLabelFontSize { get; set; } = "14px";


    private double? Temperature
    {
        get => _temperature;
        set => _temperature = value.HasValue ? Math.Round(value.Value, 1) : null;
    }

    private double? Humidity;
    private bool _displayIcon = false;
    private double? _dewPoint;

    private double? DewPoint
    {
        get => _dewPoint;
        set => _dewPoint = value.HasValue ? Math.Round(value.Value, 1) : null;
    }

    private bool _climateChartVisible = false;
    private TextOverflow TextOverflow { get; set; } = TextOverflow.None;

    #endregion

    #region Methods
    protected override async Task OnInitializedAsync()
    {
        SetInitialCoordinates();
        await LoadStationList();
        await RefreshData();

    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef ??= DotNetObjectReference.Create(this);
            await Js.InvokeVoidAsync("window.registerViewportChangeCallback", _dotNetRef);
        }
    }

    [JSInvokable]
    public void OnResize(int width, int height)
    {
        var previousWidth = ViewportWidth;

        if (previousWidth == width) return;

        ViewportWidth = width;

        UpdateViewportSettings(width);

        StateHasChanged();
    }

    private void UpdateViewportSettings(int width)
    {
        if (width < 576)
        {
            Width = "320";
            _displayIcon = true;
            TextOverflow = TextOverflow.Wrap;
            DataLabelFontSize = "10px";
            return;
        }

        if (width < 768)
        {
            Width = "520";
            _displayIcon = false;
            TextOverflow = TextOverflow.Wrap;
            DataLabelFontSize = "12px";
            return;
        }

        if (width < 992)
        {
            Width = "720";
            _displayIcon = false;
            TextOverflow = TextOverflow.Wrap;
            DataLabelFontSize = "13px";
            return;
        }

        Width = "900";
        _displayIcon = false;
        TextOverflow = TextOverflow.None;
        DataLabelFontSize = "14px";
    }

    private async Task LoadStationList()
    {
        StationData.Clear();
        var apiResponse = await ApiService.GetAllWeatherStations();

        if (apiResponse?.sources == null || apiResponse.sources.Length == 0)
        {
            ErrorMessage = "Unable to load station list. Please try again later.";
            return;
        }

        StationData.AddRange(apiResponse.sources
            .Where(weatherStation => weatherStation.StationId != null) // Filter out null StationId
            .Where(weatherStation => weatherStation.ObservationType == "synop")
            .Select(weatherStation =>
            {
                var stationName =
                    CultureInfo.CurrentCulture.TextInfo.ToTitleCase(weatherStation.StationName.ToLower().Trim());

                // Handle double names with hyphen
                if (stationName.Contains("-"))
                {
                    string[] parts = stationName.Split('-');
                    stationName = string.Join("-",
                        parts.Select(part => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(part.Trim())));
                }

                return new WeatherStations
                {
                    ID = weatherStation.StationId,
                    Text = stationName,
                    Latitude = weatherStation.Latitude,
                    Longitude = weatherStation.Longitude
                };
            })
            .GroupBy(weatherStation => new { weatherStation.ID, weatherStation.Text })
            .Select(group => group.First()));

        StationData = StationData
            .OrderBy(station => station.Text)
            .ToList();
    }
    
    private async Task RefreshData()
    {
        ErrorMessage = null;
        ClimateErrorMessage = null;
        CurrentWeather = null;
        HeatMapData = null;
        _climateChartVisible = true;

        if (!TryGetCoordinates(out var latitude, out var longitude))
        {
            ErrorMessage = "Please enter valid coordinates (lat -90 to 90, lon -180 to 180).";
            return;
        }

        Latitude = latitude;
        Longitude = longitude;
        CurrentWeather = await ApiService.GetCurrentWeatherData(latitude, longitude);

        if (CurrentWeather?.weather != null)
        {
            AssignValues();
            await GetHistoricDataForToday(latitude, longitude);
            await GetChartData(latitude, longitude);
            await GetClimateChartData();
        }
        else
        {
            ErrorMessage = "No weather data is available for these coordinates.";
        }
    }

    private async Task GetClimateChartData()
    {
        _climateChartVisible = true;
        ClimateErrorMessage = null;
        StateHasChanged();
        await Task.Yield();

        if (!TryGetCoordinates(out var latitude, out var longitude))
        {
            ClimateErrorMessage = "Please enter valid coordinates (lat -90 to 90, lon -180 to 180).";
            return;
        }

        var weatherData = await WeatherService.GetClimateChartData(latitude, longitude);

        if (weatherData.Count == 0)
        {
            ClimateErrorMessage = "No climate data is available for these coordinates.";
            return;
        }

        double[,] matrix = new double[13, 7];

        // Populate the matrix with data from weatherData
        for (int i = 0; i < 12 && i < weatherData.Count; i++)
        {
            matrix[i, 0] = Math.Round(weatherData[i].RecordLow, 1);
            matrix[i, 1] = Math.Round(weatherData[i].MonthlyLow, 1);
            matrix[i, 2] = Math.Round(weatherData[i].MinTemp, 1);
            matrix[i, 3] = Math.Round(weatherData[i].MeanTemp, 1);
            matrix[i, 4] = Math.Round(weatherData[i].MaxTemp, 1);
            matrix[i, 5] = Math.Round(weatherData[i].MonthlyHigh, 1);
            matrix[i, 6] = Math.Round(weatherData[i].RecordHigh, 1);
        }

        // Calculate aggregated data
        double minRecordLow = weatherData.Min(data => data.RecordLow);
        double avgMonthlyLow = weatherData.Average(data => data.MonthlyLow);
        double avgMinTemp = weatherData.Average(data => data.MinTemp);
        double avgMeanTemp = weatherData.Average(data => data.MeanTemp);
        double avgMaxTemp = weatherData.Average(data => data.MaxTemp);
        double avgMonthlyHigh = weatherData.Average(data => data.MonthlyHigh);
        double maxRecordHigh = weatherData.Max(data => data.RecordHigh);

        // Add aggregated data as a new row to the matrix
        matrix[12, 0] = Math.Round(minRecordLow, 1);
        matrix[12, 1] = Math.Round(avgMonthlyLow, 1);
        matrix[12, 2] = Math.Round(avgMinTemp, 1);
        matrix[12, 3] = Math.Round(avgMeanTemp, 1);
        matrix[12, 4] = Math.Round(avgMaxTemp, 1);
        matrix[12, 5] = Math.Round(avgMonthlyHigh, 1);
        matrix[12, 6] = Math.Round(maxRecordHigh, 1);

        HeatMapData = matrix;
    }

    public void OnChange(Syncfusion.Blazor.DropDowns.ChangeEventArgs<string, WeatherStations> args)
    {
        if (args.ItemData != null)
        {
            Latitude = Math.Round(args.ItemData.Latitude, 2).ToString(CultureInfo.InvariantCulture);
            Longitude = Math.Round(args.ItemData.Longitude, 2).ToString(CultureInfo.InvariantCulture);
        }
    }

    private static bool TryGetCoordinateValue(string? value, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    private bool TryGetCoordinates(out string latitude, out string longitude)
    {
        latitude = string.Empty;
        longitude = string.Empty;

        if (!TryGetCoordinateValue(Latitude, out var latitudeValue) ||
            !TryGetCoordinateValue(Longitude, out var longitudeValue))
        {
            return false;
        }

        if (latitudeValue is < -90 or > 90 || longitudeValue is < -180 or > 180)
        {
            return false;
        }

        latitude = latitudeValue.ToString("0.00", CultureInfo.InvariantCulture);
        longitude = longitudeValue.ToString("0.00", CultureInfo.InvariantCulture);
        return true;
    }

    private void SetInitialCoordinates()
    {
        Latitude = "51.04";
        Longitude = "13.74";
    }

    private async Task GetHistoricDataForToday(string latitude, string longitude)
    {
        var weatherData = await WeatherService.GetWeatherDataForDisplay(latitude, longitude);

        DisplayDailyMeans(weatherData);
    }

    private async Task GetChartData(string latitude, string longitude)
    {
        var weatherData = await WeatherService.GetChartDataForDisplay(latitude, longitude);

        PopulateChartData(weatherData);
    }

    private void PopulateChartData(IReadOnlyCollection<WeatherDataModel> groupedDayModels)
    {
        Temperatures.Clear();

        foreach (var day in groupedDayModels)
        {
            Temperatures.Add(new ChartDataModel
                { X = day.Day.ToString(), High = day.MaxTemp, Low = day.MinTemp, Precipitation = day.Precipitation });
        }
    }

    private void DisplayDailyMeans(IReadOnlyCollection<WeatherDataModel> days)
    {
        if (days.Count == 0)
        {
            ErrorMessage = "No historical data is available for these coordinates.";
            return;
        }

        CalculateDailyMeans(days);
        FindRecordHighAndLow(days);
    }

    private void CalculateDailyMeans(IReadOnlyCollection<WeatherDataModel> days)
    {
        HistoricalAverages = new WeatherDataModel
        {
            MeanTemp = CalculateAverage(days, x => x.MeanTemp),
            MaxTemp = CalculateAverage(days, x => x.MaxTemp),
            MinTemp = CalculateAverage(days, x => x.MinTemp),
            Precipitation = CalculateAverage(days, x => x.Precipitation),
            SunshineHours = CalculateAverage(days, x => x.SunshineHours),
        };
    }

    private static double CalculateAverage<T>(IEnumerable<T> collection, Func<T, double> selector)
    {
        var items = collection as IList<T> ?? collection.ToList();
        if (items.Count == 0)
        {
            return 0;
        }

        return items.Average(selector);
    }

    private void FindRecordHighAndLow(IReadOnlyCollection<WeatherDataModel> days)
    {
        var recordHighData = days.MaxBy(x => x.MaxTemp);
        var recordLowData = days.MinBy(x => x.MinTemp);

        if (recordHighData != null)
        {
            HistoricalAverages.RecordHigh = recordHighData.MaxTemp;
            HistoricalAverages.RecordHighYear = recordHighData.Year;
        }

        if (recordHighData == null || recordLowData == null) return;

        HistoricalAverages.RecordLow = recordLowData.MinTemp;
        HistoricalAverages.RecordLowYear = recordLowData.Year;
    }

    private void AssignValues()
    {
        if (CurrentWeather?.sources != null && CurrentWeather.sources.Length > 0)
        {
            StationName = CurrentWeather.sources[0].StationName;
        }
        Temperature = CurrentWeather?.weather?.Temperature;
        Humidity = CurrentWeather?.weather?.Humidity;
        DewPoint = CurrentWeather?.weather?.DewPoint;
        CloudCover = CurrentWeather?.weather?.CloudCover;
        LastUpdated = CurrentWeather.weather.TimeStamp;

        SetWeatherLogo();
    }

    private void SetWeatherLogo()
    {
        Icon = CurrentWeather?.weather?.Icon switch
        {
            "clear-day" => "sunny",
            "cloudy" => "cloudy",
            "rainy" => "rainy",
            "partly-cloudy-day" => "partly-cloudy",
            "thunderstorm" => "thunderstorm",
            "clear-night" => "clear-night",
            "partly-cloudy-night" => "cloudy-night",
            _ => Icon
        };
    }

    #endregion

    public ValueTask DisposeAsync()
    {
        _dotNetRef?.Dispose();
        return ValueTask.CompletedTask;
    }
}