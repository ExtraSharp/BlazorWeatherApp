namespace Server.Tests;

using Server.Components.Pages;
using Syncfusion.Blazor.HeatMap;

public sealed class WeatherDashboardPageStateTests
{
    [Fact]
    public void Initialize_SetsDefaultCoordinatesAndCurrentMonth()
    {
        var state = new WeatherDashboardPageState();

        state.Initialize(new DateTime(2026, 3, 23));

        Assert.Equal("51.04", state.Latitude);
        Assert.Equal("13.74", state.Longitude);
        Assert.Equal(new DateTime(2026, 3, 1), state.SelectedMonth);
    }

    [Fact]
    public void UpdateViewport_MapsBreakpointsToLayoutSettings()
    {
        var state = new WeatherDashboardPageState();

        Assert.True(state.UpdateViewport(500));
        Assert.Equal("320", state.Width);
        Assert.True(state.DisplayRefreshIcon);
        Assert.Equal(TextOverflow.Wrap, state.TextOverflow);
        Assert.Equal("10px", state.DataLabelFontSize);

        Assert.True(state.UpdateViewport(1280));
        Assert.Equal("900", state.Width);
        Assert.False(state.DisplayRefreshIcon);
        Assert.Equal(TextOverflow.None, state.TextOverflow);
        Assert.Equal("14px", state.DataLabelFontSize);
    }

    [Fact]
    public void TryNormalizeCoordinates_NormalizesCommaSeparatedValues()
    {
        var state = new WeatherDashboardPageState
        {
            Latitude = "51,04",
            Longitude = "13.74"
        };

        var valid = state.TryNormalizeCoordinates(out var latitude, out var longitude);

        Assert.True(valid);
        Assert.Equal("51.04", latitude);
        Assert.Equal("13.74", longitude);
    }

    [Fact]
    public void ApplySnapshot_SetsUiStateAndClimateMessage()
    {
        var state = new WeatherDashboardPageState();
        var snapshot = new WeatherDashboardSnapshot
        {
            CurrentWeather = new WeatherResponseModel
            {
                Weather = new WeatherModel
                {
                    TimeStamp = new DateTime(2026, 3, 23, 12, 30, 0, DateTimeKind.Utc),
                    Temperature = 15
                }
            },
            HistoricalAverages = new WeatherDataModel
            {
                MeanTemp = 11.5
            },
            Temperatures =
            [
                new ChartDataModel
                {
                    X = "1",
                    Mean = 10,
                    High = 14,
                    Low = 6,
                    Precipitation = 2
                }
            ],
            HeatMapData = null,
            StationName = "Dresden",
            LastUpdated = new DateTime(2026, 3, 23, 12, 30, 0, DateTimeKind.Utc),
            Temperature = 15,
            DewPoint = 4,
            Humidity = 55,
            CloudCover = 20,
            Icon = "sunny"
        };

        state.SetError("old error");
        state.ApplySnapshot(snapshot);

        Assert.Equal("Dresden", state.StationDisplayName);
        Assert.Equal(15, state.Temperature);
        Assert.Equal(11.5, state.HistoricalAverages.MeanTemp);
        Assert.Equal("No climate data is available for these coordinates.", state.ClimateErrorMessage);
        Assert.Null(state.ErrorMessage);
        Assert.Single(state.Temperatures);
    }
}
