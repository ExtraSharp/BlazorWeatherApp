namespace Server.Components.Pages;

internal sealed class WeatherDashboardPageState
{
    internal const string InvalidCoordinatesMessage = "Please enter valid coordinates (lat -90 to 90, lon -180 to 180).";
    internal const string StationsUnavailableMessage = "Unable to load station list. Please try again later.";
    internal const string DashboardUnavailableMessage = "No weather data is available for these coordinates.";
    internal const string DashboardLoadFailedMessage = "Unable to load weather data. Please try again later.";
    internal const string ChartLoadFailedMessage = "Unable to load chart data. Please try again later.";
    private const string ClimateUnavailableMessage = "No climate data is available for these coordinates.";
    private const string DefaultLatitude = "51.04";
    private const string DefaultLongitude = "13.74";

    private static readonly WeatherDashboardViewportSettings DesktopViewport = new("900", false, TextOverflow.None, "14px");

    public double[,]? HeatMapData { get; private set; }
    public string? ClimateErrorMessage { get; private set; }
    public DateTime LastUpdated { get; private set; }
    public string Width { get; private set; } = DesktopViewport.Width;
    public IReadOnlyList<ChartDataModel> Temperatures { get; private set; } = [];
    public int ViewportWidth { get; private set; }
    public WeatherResponseModel? CurrentWeather { get; private set; }
    public string? StationName { get; private set; }
    public string? ErrorMessage { get; private set; }
    public WeatherDataModel HistoricalAverages { get; private set; } = new();
    public double? CloudCover { get; private set; }
    public string Latitude { get; set; } = DefaultLatitude;
    public IReadOnlyList<WeatherStationOption> StationData { get; private set; } = [];
    public string Longitude { get; set; } = DefaultLongitude;
    public string? Icon { get; private set; }
    public double? Temperature { get; private set; }
    public string DataLabelFontSize { get; private set; } = DesktopViewport.DataLabelFontSize;
    public DateTime SelectedMonth { get; private set; }
    public double? Humidity { get; private set; }
    public bool DisplayRefreshIcon { get; private set; }
    public double? DewPoint { get; private set; }
    public TextOverflow TextOverflow { get; private set; } = DesktopViewport.TextOverflow;
    public bool IsRefreshing { get; set; }
    public bool IsStationsLoading { get; set; }
    public bool IsMonthNavigationLoading { get; set; }
    public bool IsBusy => IsRefreshing || IsMonthNavigationLoading;
    public string StationDisplayName => !string.IsNullOrWhiteSpace(StationName) ? StationName : "Not selected";
    public string LastUpdatedTime =>
        LastUpdated == default
            ? "--:--"
            : LastUpdated.ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture);
    public string SelectedMonthLabel =>
        SelectedMonth == default
            ? string.Empty
            : SelectedMonth.ToString("MMMM", CultureInfo.CurrentCulture);

    public void Initialize(DateTime today)
    {
        Latitude = DefaultLatitude;
        Longitude = DefaultLongitude;
        SelectedMonth = CreateMonthAnchor(today);
    }

    public bool UpdateViewport(int width)
    {
        if (ViewportWidth == width)
        {
            return false;
        }

        ViewportWidth = width;

        var settings = ResolveViewportSettings(width);
        Width = settings.Width;
        DisplayRefreshIcon = settings.DisplayRefreshIcon;
        TextOverflow = settings.TextOverflow;
        DataLabelFontSize = settings.DataLabelFontSize;

        return true;
    }

    public void SetStationOptions(IReadOnlyList<WeatherStationOption> stationData) =>
        StationData = stationData;

    public void SetStationCoordinates(WeatherStationOption station)
    {
        Latitude = Math.Round(station.Latitude, 2).ToString(CultureInfo.InvariantCulture);
        Longitude = Math.Round(station.Longitude, 2).ToString(CultureInfo.InvariantCulture);
    }

    public void SetSelectedMonth(DateTime date) =>
        SelectedMonth = CreateMonthAnchor(date);

    public void ShiftSelectedMonth(int offset) =>
        SelectedMonth = SelectedMonth.AddMonths(offset);

    public void PrepareForDashboardLoad()
    {
        ErrorMessage = null;
        ClimateErrorMessage = null;
        CurrentWeather = null;
        HeatMapData = null;
    }

    public void ApplyNormalizedCoordinates(string latitude, string longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public bool TryNormalizeCoordinates(out string latitude, out string longitude)
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

    public void ApplySnapshot(WeatherDashboardSnapshot snapshot)
    {
        CurrentWeather = snapshot.CurrentWeather;
        HistoricalAverages = snapshot.HistoricalAverages;
        Temperatures = snapshot.Temperatures;
        HeatMapData = snapshot.HeatMapData;
        StationName = snapshot.StationName;
        Temperature = snapshot.Temperature;
        Humidity = snapshot.Humidity;
        DewPoint = snapshot.DewPoint;
        CloudCover = snapshot.CloudCover;
        LastUpdated = snapshot.LastUpdated;
        Icon = snapshot.Icon;
        ClimateErrorMessage = snapshot.HeatMapData is null ? ClimateUnavailableMessage : null;
        ErrorMessage = null;
    }

    public void SetMonthlyChart(IReadOnlyList<ChartDataModel> chartData)
    {
        Temperatures = chartData;
        ErrorMessage = null;
    }

    public void SetError(string? errorMessage) =>
        ErrorMessage = errorMessage;

    internal static string FormatDayLabel(DateTime date) =>
        date.ToString("dd MMM", CultureInfo.CurrentCulture);

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

    private static DateTime CreateMonthAnchor(DateTime date) =>
        new(date.Year, date.Month, 1);

    private static WeatherDashboardViewportSettings ResolveViewportSettings(int width) =>
        width switch
        {
            < 576 => new WeatherDashboardViewportSettings("320", true, TextOverflow.Wrap, "10px"),
            < 768 => new WeatherDashboardViewportSettings("520", false, TextOverflow.Wrap, "12px"),
            < 992 => new WeatherDashboardViewportSettings("720", false, TextOverflow.Wrap, "13px"),
            _ => DesktopViewport
        };

    private sealed record WeatherDashboardViewportSettings(
        string Width,
        bool DisplayRefreshIcon,
        TextOverflow TextOverflow,
        string DataLabelFontSize);
}
