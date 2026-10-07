namespace Server;

public sealed class WeatherService(ApiService apiService, TimeProvider timeProvider)
{
    private readonly ApiService _apiService = apiService;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<IReadOnlyCollection<WeatherDataModel>> GetChartDataForDisplay(
        string latitude,
        string longitude,
        DateTime? targetDate = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveDate = targetDate ?? _timeProvider.GetLocalNow().DateTime.Date;
        var entireMonth = await FetchEntireMonth(effectiveDate, latitude, longitude, cancellationToken);
        var dayModels = CreateDayModels(entireMonth);

        return GroupDayModels(dayModels);
    }

    public async Task<IReadOnlyList<WeatherDataModel>> GetClimateChartData(
        string latitude,
        string longitude,
        CancellationToken cancellationToken = default)
    {
        var latestAvailableDate = _timeProvider.GetLocalNow().DateTime.Date;
        var climateSourceData = await FetchClimateSourceData(latitude, longitude, latestAvailableDate, cancellationToken);
        return BuildClimateChartData(climateSourceData);
    }

    internal static List<WeatherDataModel> BuildClimateChartData(IReadOnlyList<IReadOnlyCollection<WeatherModel>> yearlyData)
    {
        List<WeatherDataModel> output = new(12);

        for (int month = 1; month <= 12; month++)
        {
            var chartData = yearlyData
                .Select(yearData => yearData.Where(data => data.TimeStamp.Month == month).ToList())
                .Select(CalculateMonthlyAverages)
                .Where(monthlyAverages => monthlyAverages.Count > 0)
                .Select(CalculateMonthlySummary)
                .ToList();

            output.Add(CalculateFinalSummary(chartData));
        }

        return output;
    }

    internal static List<WeatherDataModel> CalculateMonthlyAverages(IReadOnlyCollection<WeatherModel> yearData)
    {
        var filtered = yearData
            .Where(data => data.Temperature != null)
            .ToList();

        if (filtered.Count == 0)
        {
            return new List<WeatherDataModel>();
        }

        return filtered
            .GroupBy(data => new { data.TimeStamp.Year, data.TimeStamp.Month, data.TimeStamp.Day })
            .Select(group => new WeatherDataModel
            {
                Year = group.Key.Year,
                Month = group.Key.Month,
                MaxTemp = group.Max(data => data.Temperature!.Value),
                MinTemp = group.Min(data => data.Temperature!.Value),
                MeanTemp = group.Average(data => data.Temperature!.Value),
                Precipitation = group.Sum(data => data.Precipitation ?? 0),
                SunshineHours = group.Sum(data => (data.SunshineHours ?? 0) / 60)
            })
            .ToList();
    }


    internal static WeatherDataModel CalculateMonthlySummary(IReadOnlyCollection<WeatherDataModel> monthlyAverages)
    {
        if (monthlyAverages.Count == 0)
        {
            return new WeatherDataModel();
        }

        var monthData = new WeatherDataModel
        {
            MaxTemp = monthlyAverages.Average(day => day.MaxTemp),
            MeanTemp = monthlyAverages.Average(day => day.MeanTemp),
            MinTemp = monthlyAverages.Average(day => day.MinTemp),
            MonthlyHigh = monthlyAverages.Max(day => day.MaxTemp),
            MonthlyLow = monthlyAverages.Min(day => day.MinTemp),
            Precipitation = monthlyAverages.Sum(day => day.Precipitation),
            SunshineHours = monthlyAverages.Sum(day => day.SunshineHours)
        };

        return monthData;
    }

    internal static WeatherDataModel CalculateFinalSummary(IReadOnlyCollection<WeatherDataModel> chartData)
    {
        if (chartData.Count == 0)
        {
            return new WeatherDataModel();
        }

        var finalData = new WeatherDataModel
        {
            RecordHigh = chartData.Max(day => day.MonthlyHigh),
            RecordLow = chartData.Min(day => day.MonthlyLow),
            MonthlyHigh = chartData.Average(day => day.MonthlyHigh),
            MonthlyLow = chartData.Average(day => day.MonthlyLow),
            MaxTemp = chartData.Average(day => day.MaxTemp),
            MeanTemp = chartData.Average(day => day.MeanTemp),
            MinTemp = chartData.Average(day => day.MinTemp),
            Precipitation = chartData.Average(day => day.Precipitation),
            SunshineHours = chartData.Average(day => day.SunshineHours)
        };

        return finalData;
    }

    internal static bool ShouldRequestRange(DateTime rangeStart, DateTime latestAvailableDate) =>
        rangeStart.Date <= latestAvailableDate.Date;

    private async Task<IReadOnlyList<IReadOnlyCollection<WeatherModel>>> FetchClimateSourceData(
        string latitude,
        string longitude,
        DateTime latestAvailableDate,
        CancellationToken cancellationToken)
    {
        var tasks = Enumerable
            .Range(0, 5)
            .Select(offset => FetchYearRange(latestAvailableDate.Year - offset, latestAvailableDate, latitude, longitude, cancellationToken))
            .ToArray();

        return await Task.WhenAll(tasks);
    }

    private async Task<List<WeatherModel>> FetchYearRange(
        int year,
        DateTime latestAvailableDate,
        string latitude,
        string longitude,
        CancellationToken cancellationToken)
    {
        var rangeStart = new DateTime(year, 1, 1);

        if (!ShouldRequestRange(rangeStart, latestAvailableDate))
        {
            return [];
        }

        var rangeEnd = year == latestAvailableDate.Year
            ? latestAvailableDate
            : new DateTime(year, 12, 31);

        var response = await _apiService.GetWeatherRangeData(
            rangeStart.ToString("yyyy-MM-dd"),
            rangeEnd.ToString("yyyy-MM-dd"),
            latitude,
            longitude,
            cancellationToken);

        var weatherData = response?.Weather ?? [];

        return weatherData
            .OfType<WeatherModel>()
            .ToList();
    }

    private async Task<List<WeatherModel>> FetchEntireMonth(
        DateTime desiredDate,
        string latitude,
        string longitude,
        CancellationToken cancellationToken)
    {
        var responses = await FetchHistoricalWeatherData(desiredDate, true, latitude, longitude, cancellationToken);

        return responses
            .Where(r => r != null)
            .SelectMany(r => r?.Weather ?? [])
            .OfType<WeatherModel>()
            .Where(weather => weather.TimeStamp.Month == desiredDate.Month)
            .ToList();
    }

    internal static IReadOnlyList<WeatherDataModel> CreateDayModels(IEnumerable<WeatherModel> mergedList)
    {
        return mergedList
            .Where(weather => weather.Temperature.HasValue)
            .GroupBy(weather => weather.TimeStamp.Date)
            .Select(group => new WeatherDataModel
            {
                Day = group.Key.Day,
                Month = group.Key.Month,
                Year = group.Key.Year,
                MaxTemp = group.Max(weather => weather.Temperature!.Value),
                MinTemp = group.Min(weather => weather.Temperature!.Value),
                MeanTemp = group.Average(weather => weather.Temperature!.Value),
                Precipitation = group.Sum(weather => weather.Precipitation ?? 0)
            })
            .ToList();
    }

    internal static List<WeatherDataModel> GroupDayModels(IEnumerable<WeatherDataModel> dayModels)
    {
        return dayModels
            .GroupBy(dayModel => dayModel.Day)
            .Select(group => new WeatherDataModel
            {
                Day = group.Key,
                MaxTemp = group.Average(dayModel => dayModel.MaxTemp),
                MinTemp = group.Average(dayModel => dayModel.MinTemp),
                MeanTemp = group.Average(dayModel => dayModel.MeanTemp),
                Precipitation = group.Average(dayModel => dayModel.Precipitation)
            })
            .ToList();
    }

    public async Task<IReadOnlyCollection<WeatherDataModel>> GetWeatherDataForDisplay(
        string latitude,
        string longitude,
        CancellationToken cancellationToken = default)
    {
        var desiredDate = _timeProvider.GetLocalNow().DateTime.Date;

        var responses = await FetchHistoricalWeatherData(desiredDate, false, latitude, longitude, cancellationToken);
        var mergedResponse = MergeWeatherResponses(responses, desiredDate.Day);

        return CalculateDailyMeans(mergedResponse);
    }

    public async Task<MultipleWeatherResponseModel?[]> FetchHistoricalWeatherData(
        DateTime desiredDate,
        bool monthly,
        string latitude,
        string longitude,
        CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task<MultipleWeatherResponseModel?>>();

        if (monthly)
        {
            var latestAvailableDate = _timeProvider.GetLocalNow().DateTime.Date;

            for (var i = 0; i <= 20; i++)
            {
                var yearToSubtract = desiredDate.Year - i;
                var firstDayOfYear = new DateTime(yearToSubtract, desiredDate.Month, 1);

                if (!ShouldRequestRange(firstDayOfYear, latestAvailableDate))
                {
                    continue;
                }

                var lastDayOfYear = firstDayOfYear.AddMonths(1);
                tasks.Add(_apiService.GetWeatherRangeData(
                    firstDayOfYear.ToString("yyyy-MM-dd"),
                    lastDayOfYear.ToString("yyyy-MM-dd"),
                    latitude,
                    longitude,
                    cancellationToken));
            }
        }
        else
        {
            for (var i = 0; i < 20; i++)
            {
                var dateString = desiredDate.AddYears(-i).ToString("yyyy-MM-dd");
                tasks.Add(_apiService.GetHistoricalWeatherData(dateString, latitude, longitude, cancellationToken));
            }
        }

        return await Task.WhenAll(tasks);
    }

    internal static MultipleWeatherResponseModel MergeWeatherResponses(IEnumerable<MultipleWeatherResponseModel?> responses, int day)
    {
        var weatherList = responses
            .Where(response => response?.Weather is { Length: > 0 })
            .SelectMany(response => response?.Weather ?? [])
            .OfType<WeatherModel>()
            .Where(weather => weather.TimeStamp.Day == day)
            .GroupBy(weather => weather.TimeStamp)
            .Select(group => group.First())
            .ToList();

        return new MultipleWeatherResponseModel
        {
            Weather = weatherList.Cast<WeatherModel?>().ToArray()
        };
    }

    internal static List<WeatherDataModel> CalculateDailyMeans(MultipleWeatherResponseModel mergedResponse)
    {
        if (mergedResponse.Weather.Length == 0)
        {
            return new List<WeatherDataModel>();
        }

        var groupedByDay = mergedResponse.Weather
            .OfType<WeatherModel>()
            .GroupBy(w => w.TimeStamp.Date);

        return groupedByDay.Select(group => new WeatherDataModel
            {
                Day = group.Key.Day,
                Month = group.Key.Month,
                Year = group.Key.Year,
                MeanTemp = group.Average(w => w.Temperature ?? 0),
                MaxTemp = group.Max(w => w.Temperature ?? 0),
                MinTemp = group.Min(w => w.Temperature ?? 0),
                Precipitation = group.Sum(w => w.Precipitation ?? 0),
                SunshineHours = group.Sum(w => (w.SunshineHours ?? 0) / 60)
            })
            .ToList();
    }
}
