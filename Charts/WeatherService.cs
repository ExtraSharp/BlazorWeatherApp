namespace Server;

public class WeatherService(ApiService apiService)
{
    private readonly ApiService _apiService = apiService;

    public async Task<IReadOnlyCollection<WeatherDataModel>> GetChartDataForDisplay(string latitude, string longitude)
    {
        var entireMonth = await FetchEntireMonth(latitude, longitude);
        var dayModels = CreateDayModels(entireMonth);

        return GroupDayModels(dayModels);
    }

    public async Task<List<WeatherDataModel>> GetClimateChartData(string latitude, string longitude)
    {
        const int year = 2023;
        const int maxConcurrentMonths = 4;
        List<WeatherDataModel> output = new();

        using var monthThrottle = new SemaphoreSlim(maxConcurrentMonths);
        var monthTasks = Enumerable.Range(1, 12)
            .Select(async month =>
            {
                await monthThrottle.WaitAsync();
                try
                {
                    var yearTasks = Enumerable.Range(0, 5)
                        .Select(i => FetchEntireYear(year - i, month, latitude, longitude))
                        .ToArray();

                    var yearDataSets = await Task.WhenAll(yearTasks);
                    var chartData = yearDataSets
                        .Select(data => CalculateMonthlySummary(CalculateMonthlyAverages(data)))
                        .ToList();

                    var finalData = CalculateFinalSummary(chartData);
                    return (month, finalData);
                }
                finally
                {
                    monthThrottle.Release();
                }
            })
            .ToArray();

        var monthResults = await Task.WhenAll(monthTasks);
        output.AddRange(monthResults
            .OrderBy(result => result.month)
            .Select(result => result.finalData));

        return output;
    }

    private List<WeatherDataModel> CalculateMonthlyAverages(List<WeatherModel> yearData)
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
                MaxTemp = (double)group.Max(data => data.Temperature),
                MinTemp = (double)group.Min(data => data.Temperature),
                MeanTemp = (double)group.Average(data => data.Temperature),
                //AverageHigh = group.Average(data => data.Temperature),
                // Calculate other statistics as needed
            })
            .ToList();
    }


    private WeatherDataModel CalculateMonthlySummary(List<WeatherDataModel> monthlyAverages)
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
            MonthlyLow = monthlyAverages.Min(day => day.MinTemp)
        };

        return monthData;
    }

    private WeatherDataModel CalculateFinalSummary(List<WeatherDataModel> chartData)
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
            MinTemp = chartData.Average(day => day.MinTemp)
        };

        return finalData;
    }

    private async Task<List<WeatherModel>> FetchEntireYear(int year, int month, string latitude, string longitude)
    {
        int days = DateTime.DaysInMonth(year, month);
        var response = await _apiService.GetEntireData(
            $"{year}-{month:00}-01",
            $"{year}-{month:00}-{days:00}",
            latitude,
            longitude);

        var weatherData = response?.weather ?? Enumerable.Empty<WeatherModel>();

        return weatherData.ToList();
    }

    private async Task<List<WeatherModel>> FetchEntireMonth(string latitude, string longitude)
    {
        var today = DateTime.Today;
        var desiredDate = new DateTime(today.Year, today.Month, today.Day);

        var responses = await FetchHistoricalWeatherData(desiredDate, true, latitude, longitude);

        return responses
            .Where(r => r != null)
            .SelectMany(r => r.weather ?? Enumerable.Empty<WeatherModel>())
            .Where(weather => weather.TimeStamp.Month == today.Month) // Filter by month
            .ToList();
    }

    private static IEnumerable<WeatherDataModel> CreateDayModels(IEnumerable<WeatherModel> mergedList)
    {
        return mergedList
            .Where(weather => weather.Temperature.HasValue)
            .GroupBy(weather => new { Day = weather.TimeStamp.Day, Month = weather.TimeStamp.Month, Year = weather.TimeStamp.Year })
            .Select(group => new WeatherDataModel
            {
                Day = group.First().TimeStamp.Day,
                Month = group.First().TimeStamp.Month,
                Year = group.First().TimeStamp.Year,
                MaxTemp = (double)group.Max(weather => weather.Temperature),
                MinTemp = (double)group.Min(weather => weather.Temperature),
                Precipitation = group.Average(weather => weather.Precipitation ?? double.MinValue)
            })
            .ToList();
    }

    private static List<WeatherDataModel> GroupDayModels(IEnumerable<WeatherDataModel> dayModels)
    {
        return dayModels
            .GroupBy(dayModel => dayModel.Day)
            .Select(group => new WeatherDataModel
            {
                Day = group.Key,
                MaxTemp = group.Average(dayModel => dayModel.MaxTemp),
                MinTemp = group.Average(dayModel => dayModel.MinTemp),
                Precipitation = group.Sum(dayModel => dayModel.Precipitation)
                // Include other properties as needed
            })
            .ToList();
    }

    public async Task<IReadOnlyCollection<WeatherDataModel>> GetWeatherDataForDisplay(string latitude, string longitude)
    {
        var desiredDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);

        var responses = await FetchHistoricalWeatherData(desiredDate, false, latitude, longitude);
        var mergedResponse = MergeWeatherResponses(responses, desiredDate.Day);

        return CalculateDailyMeans(mergedResponse);
    }

    public async Task<MultipleWeatherResponseModel?[]> FetchHistoricalWeatherData(
        DateTime desiredDate,
        bool monthly,
        string latitude,
        string longitude)
    {
        var tasks = new List<Task<MultipleWeatherResponseModel?>>();

        if (monthly)
        {
            var firstDayOfMonth = new DateTime(desiredDate.Year, desiredDate.Month, 1);
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1);

            tasks.Add(_apiService.GetMonthlyData(
                firstDayOfMonth.ToString("yyyy-MM-dd"),
                lastDayOfMonth.ToString("yyyy-MM-dd"),
                latitude,
                longitude));

            // Loop for going back year by year
            for (var i = 1; i <= 20; i++)
            {
                var yearToSubtract = desiredDate.Year - i;
                var firstDayOfYear = new DateTime(yearToSubtract, desiredDate.Month, 1);
                var lastDayOfYear = firstDayOfYear.AddMonths(1);
                tasks.Add(_apiService.GetMonthlyData(
                    firstDayOfYear.ToString("yyyy-MM-dd"),
                    lastDayOfYear.ToString("yyyy-MM-dd"),
                    latitude,
                    longitude));
            }
        }
        else
        {
            for (var i = 0; i < 20; i++)
            {
                var dateString = desiredDate.AddYears(-i).ToString("yyyy-MM-dd");
                tasks.Add(_apiService.GetHistoricalWeatherData(dateString, latitude, longitude));
            }
        }

        return await Task.WhenAll(tasks);
    }

    private static MultipleWeatherResponseModel MergeWeatherResponses(IEnumerable<MultipleWeatherResponseModel?> responses, int day)
    {
        var mergedResponse = new MultipleWeatherResponseModel();

        var weatherList = responses
            .Where(response => response?.weather != null)
            .SelectMany(response => response?.weather ?? Array.Empty<WeatherModel>())
            .Where(weather => weather?.TimeStamp.Day == day)
            .GroupBy(weather => weather?.TimeStamp)
            .Select(group => group.First())
            .ToList();

        mergedResponse.weather = weatherList.ToArray();

        return mergedResponse;
    }

    private static List<WeatherDataModel> CalculateDailyMeans(MultipleWeatherResponseModel mergedResponse)
    {
        if (mergedResponse.weather == null || mergedResponse.weather.Length == 0)
        {
            return new List<WeatherDataModel>();
        }

        var groupedByDay = mergedResponse.weather
            .Where(w => w != null)
            .GroupBy(w => w!.TimeStamp.Date);

        return groupedByDay.Select(group => new WeatherDataModel
            {
                Day = group.Key.Day,
                Month = group.Key.Month,
                Year = group.Key.Year,
                MeanTemp = group.Average(w => w?.Temperature ?? 0),
                MaxTemp = group.Max(w => w?.Temperature ?? 0),
                MinTemp = group.Min(w => w?.Temperature ?? 0),
                Precipitation = group.Sum(w => w?.Precipitation ?? 0),
                SunshineHours = group.Sum(w => w?.SunshineHours / 60 ?? 0)
            })
            .ToList();
    }
}
