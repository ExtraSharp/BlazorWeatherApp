namespace Server.Tests;

public sealed class WeatherDashboardServiceTests
{
    [Fact]
    public void CreateHistoricalAverages_CalculatesMeansAndRecordYears()
    {
        IReadOnlyCollection<WeatherDataModel> days =
        [
            new WeatherDataModel
            {
                Year = 2020,
                MaxTemp = 20,
                MinTemp = 5,
                MeanTemp = 12,
                Precipitation = 3,
                SunshineHours = 6
            },
            new WeatherDataModel
            {
                Year = 2021,
                MaxTemp = 24,
                MinTemp = 2,
                MeanTemp = 14,
                Precipitation = 1,
                SunshineHours = 8
            }
        ];

        var summary = WeatherDashboardService.CreateHistoricalAverages(days);

        Assert.Equal(22, summary.MaxTemp);
        Assert.Equal(3.5, summary.MinTemp);
        Assert.Equal(13, summary.MeanTemp);
        Assert.Equal(2, summary.Precipitation);
        Assert.Equal(7, summary.SunshineHours);
        Assert.Equal(24, summary.RecordHigh);
        Assert.Equal(2021, summary.RecordHighYear);
        Assert.Equal(2, summary.RecordLow);
        Assert.Equal(2021, summary.RecordLowYear);
    }

    [Fact]
    public void NormalizeStationName_PreservesHyphenatedCasing()
    {
        var normalized = WeatherDashboardService.NormalizeStationName("frankfurt-main west");

        Assert.Equal("Frankfurt-Main West", normalized);
    }
}
