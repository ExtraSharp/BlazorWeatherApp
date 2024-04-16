﻿namespace Server.Components.Pages;

public partial class Index
{
    #region Private Members
    public class WeatherStations
    {
        public string ID { get; set; }
        public string Text { get; set; }
        public float Latitude { get; set; }
        public float Longitude { get; set; }
    }

    private DateTime LastUpdated { get; set; }

    private string LastUpdatedTime
    {
        get
        {
            var cstTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(LastUpdated, "Central European Standard Time");
            return cstTime.ToString("HH:mm");
        }
    }

    private WeatherService? _weatherService;
    private string Width { get; set; } = "730";
    private List<ChartDataModel> Temperatures = [];
    private int ViewportWidth { get; set; }
    private int ViewportHeight { get; set; }
    private WeatherResponseModel? CurrentWeather { get; set; }
    private string? StationName { get; set; }
    private bool IsDataAvailable { get; set; } = true;
    private WeatherDataModel HistoricalAverages { get; set; } = new();
    private double? CloudCover { get; set; }
    private string Latitude { get; set; }
    private List<WeatherStations> StationData = new List<WeatherStations>();
    private string Longitude { get; set; }
    private string? Icon { get; set; }
    private static bool _bigWindowSize = true;
    private double? _temperature;

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
            StateHasChanged();
            await Js.InvokeVoidAsync("window.registerViewportChangeCallback", DotNetObjectReference.Create(this));
            // var dimension = await Js.InvokeAsync<WindowDimension>("getWindowDimensions");
        }
    }

    [JSInvokable]
    public void OnResize(int width, int height)
    {
        if (ViewportWidth == width && ViewportHeight == height) return;

        ViewportWidth = width;
        ViewportHeight = height;

        switch (ViewportWidth)
        {
            case < 410:
                Width = "270";
                _displayIcon = true;
                break;
            case < 468:
                Width = "300";
                _displayIcon = false;
                break;
            case < 769:
                Width = "500";
                _displayIcon = false;
                break;
            case < 1000:
                Width = "730";
                _displayIcon = false;
                break;
            case >= 1000:
                Width = "730";
                _displayIcon = false;
                break;
            default:
                Width = "730";
                _displayIcon = false;
                break;
        }

        StateHasChanged();
    }

    private async Task LoadStationList()
    {
        StationData.Clear();
        var apiResponse = await ApiService.GetAllWeatherStations();

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
        CurrentWeather = null;

        if (string.IsNullOrWhiteSpace(Latitude) || string.IsNullOrWhiteSpace(Longitude) || !IsValidCoordinates())
        {
            // Handle validation errors
            return;
        }

        _weatherService = new WeatherService(Latitude, Longitude);

        CurrentWeather = await ApiService.GetCurrentWeatherData(Latitude, Longitude);

        if (CurrentWeather?.weather != null)
        {
            AssignValues();
            await GetHistoricDataForToday();
            await GetChartData();
            IsDataAvailable = true;
        }
        else
        {
            IsDataAvailable = false;
        }
    }

    public void OnChange(Syncfusion.Blazor.DropDowns.ChangeEventArgs<string, WeatherStations> args)
    {
        if (args.ItemData != null)
        {
            Latitude = Math.Round(args.ItemData.Latitude, 2).ToString();
            Longitude = Math.Round(args.ItemData.Longitude, 2).ToString();
        }
    }

    private bool IsValidCoordinates()
    {
        var latitude = double.Parse(Latitude);
        var longitude = double.Parse(Longitude);

        return latitude is <= 90 and >= -90 && longitude is <= 180 and >= -180;
    }

    private void SetInitialCoordinates()
    {
        Latitude = "51.04";
        Longitude = "13.74";
    }

    private async Task GetHistoricDataForToday()
    {
        if (_weatherService != null)
        {
            var weatherData = await _weatherService.GetWeatherDataForDisplay();

            DisplayDailyMeans(weatherData);
        }
    }

    private async Task GetChartData()
    {
        if (_weatherService != null)
        {
            var weatherData = await _weatherService.GetChartDataForDisplay();

            PopulateChartData(weatherData);
        }
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
        return collection.Average(selector);
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
        if (CurrentWeather?.sources != null) StationName = CurrentWeather?.sources[0].StationName;
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
}