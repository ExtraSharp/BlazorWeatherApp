namespace Server.Tests;

public sealed class WeatherServiceTests
{
    [Fact]
    public void GroupDayModels_AveragesPrecipitationAcrossYears()
    {
        IReadOnlyCollection<WeatherDataModel> input =
        [
            new WeatherDataModel
            {
                Day = 1,
                MaxTemp = 10,
                MinTemp = 2,
                MeanTemp = 6,
                Precipitation = 12
            },
            new WeatherDataModel
            {
                Day = 1,
                MaxTemp = 14,
                MinTemp = 4,
                MeanTemp = 8,
                Precipitation = 18
            }
        ];

        var grouped = WeatherService.GroupDayModels(input);

        var day = Assert.Single(grouped);

        Assert.Equal(12, day.MaxTemp);
        Assert.Equal(3, day.MinTemp);
        Assert.Equal(7, day.MeanTemp);
        Assert.Equal(15, day.Precipitation);
    }

    [Fact]
    public void CalculateMonthlyAverages_IgnoresRowsWithoutTemperature()
    {
        IReadOnlyCollection<WeatherModel> weather =
        [
            new WeatherModel
            {
                TimeStamp = new DateTime(2024, 1, 1, 6, 0, 0, DateTimeKind.Utc),
                Temperature = 10,
                Precipitation = 1,
                SunshineHours = 60
            },
            new WeatherModel
            {
                TimeStamp = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                Temperature = 14,
                Precipitation = 2,
                SunshineHours = 30
            },
            new WeatherModel
            {
                TimeStamp = new DateTime(2024, 1, 2, 12, 0, 0, DateTimeKind.Utc),
                Temperature = null,
                Precipitation = 10,
                SunshineHours = 120
            }
        ];

        var monthlyAverages = WeatherService.CalculateMonthlyAverages(weather);

        var day = Assert.Single(monthlyAverages);
        Assert.Equal(14, day.MaxTemp);
        Assert.Equal(10, day.MinTemp);
        Assert.Equal(12, day.MeanTemp);
        Assert.Equal(3, day.Precipitation);
        Assert.Equal(1.5, day.SunshineHours);
    }

    [Fact]
    public void ShouldRequestRange_SkipsFutureMonths()
    {
        var latestAvailableDate = new DateTime(2026, 3, 23);

        Assert.True(WeatherService.ShouldRequestRange(new DateTime(2026, 3, 1), latestAvailableDate));
        Assert.False(WeatherService.ShouldRequestRange(new DateTime(2026, 4, 1), latestAvailableDate));
    }

    [Fact]
    public void BuildClimateChartData_AggregatesYearlyRangesIntoMonthlySummaries()
    {
        IReadOnlyList<IReadOnlyCollection<WeatherModel>> yearlyData =
        [
            [
                new WeatherModel
                {
                    TimeStamp = new DateTime(2024, 1, 1, 6, 0, 0, DateTimeKind.Utc),
                    Temperature = 10,
                    Precipitation = 1,
                    SunshineHours = 60
                },
                new WeatherModel
                {
                    TimeStamp = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                    Temperature = 14,
                    Precipitation = 2,
                    SunshineHours = 30
                },
                new WeatherModel
                {
                    TimeStamp = new DateTime(2024, 2, 1, 12, 0, 0, DateTimeKind.Utc),
                    Temperature = 4,
                    Precipitation = 8,
                    SunshineHours = 120
                }
            ],
            [
                new WeatherModel
                {
                    TimeStamp = new DateTime(2025, 1, 1, 6, 0, 0, DateTimeKind.Utc),
                    Temperature = 20,
                    Precipitation = 3,
                    SunshineHours = 0
                },
                new WeatherModel
                {
                    TimeStamp = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                    Temperature = 22,
                    Precipitation = 1,
                    SunshineHours = 60
                },
                new WeatherModel
                {
                    TimeStamp = new DateTime(2025, 2, 1, 12, 0, 0, DateTimeKind.Utc),
                    Temperature = 8,
                    Precipitation = 2,
                    SunshineHours = 180
                }
            ]
        ];

        var climate = WeatherService.BuildClimateChartData(yearlyData);

        Assert.Equal(12, climate.Count);

        var january = climate[0];
        Assert.Equal(22, january.RecordHigh);
        Assert.Equal(10, january.RecordLow);
        Assert.Equal(16.5, january.MeanTemp);
        Assert.Equal(3.5, january.Precipitation);
        Assert.Equal(1.25, january.SunshineHours);

        var february = climate[1];
        Assert.Equal(8, february.RecordHigh);
        Assert.Equal(4, february.RecordLow);
        Assert.Equal(6, february.MeanTemp);
        Assert.Equal(5, february.Precipitation);
        Assert.Equal(2.5, february.SunshineHours);
    }
}
