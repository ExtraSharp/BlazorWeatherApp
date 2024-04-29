namespace Server;

public class WeatherService(string latitude, string longitude)
{
    private readonly ApiService _apiService = new();

    public async Task<IReadOnlyCollection<WeatherDataModel>> GetChartDataForDisplay()
    {
        var entireMonth = await FetchEntireMonth();
        var dayModels = CreateDayModels(entireMonth);

        return GroupDayModels(dayModels);
    }

    public async Task<List<WeatherDataModel>> GetClimateChartData()
    {
        int year = 2023;
        
        List<WeatherDataModel> output = new();

        for (int j = 1; j <= 12; j++)
        {
            year = 2023;

            List<WeatherDataModel> monthlyAverages = new();
            List<WeatherDataModel> chartData = new();

            for (int i = 0; i < 5; i++)
            {
                List<WeatherModel> yearData = await FetchEntireYear(year - i, j);

                monthlyAverages = CalculateMonthlyAverages(yearData);

                WeatherDataModel monthData = CalculateMonthlySummary(monthlyAverages);

                chartData.Add(monthData);
            }

            WeatherDataModel finalData = CalculateFinalSummary(chartData);
            output.Add(finalData);
        }
        
        

        return output;
    }

    private List<WeatherDataModel> CalculateMonthlyAverages(List<WeatherModel> yearData)
    {
        return yearData
            .Where(data => data.Temperature != null)
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

    private async Task<List<WeatherModel>> FetchEntireYear(int year, int month)
    {
        var tasks = new List<Task<MultipleWeatherResponseModel?>>();

        int days = DateTime.DaysInMonth(year, month);

        tasks.Add(_apiService.GetEntireData($"{year.ToString()}-{month:00}-01", $"{year.ToString()}-{month:00}-{days:00}", latitude, longitude));

        var responses = await Task.WhenAll(tasks);
        var weatherData = responses.SelectMany(response => response?.weather ?? Enumerable.Empty<WeatherModel>());

        return weatherData.ToList();

    }

    private async Task<List<WeatherModel>> FetchEntireMonth()
    {
        var today = DateTime.Today;
        var desiredDate = new DateTime(today.Year, today.Month, today.Day);

        var responses = await FetchHistoricalWeatherData(desiredDate, true);

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

    public async Task<IReadOnlyCollection<WeatherDataModel>> GetWeatherDataForDisplay()
    {
        var desiredDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);

        var responses = await FetchHistoricalWeatherData(desiredDate);
        var mergedResponse = MergeWeatherResponses(responses, desiredDate.Day);

        return CalculateDailyMeans(mergedResponse);
    }

    public async Task<MultipleWeatherResponseModel?[]> FetchHistoricalWeatherData(DateTime desiredDate, bool monthly = false)
    {
        var tasks = new List<Task<MultipleWeatherResponseModel?>>();

        if (monthly)
        {
            var firstDayOfMonth = new DateTime(desiredDate.Year, desiredDate.Month, 1);
            var lastDayOfMonth = new DateTime(desiredDate.Year, desiredDate.Month + 1, 1);

            tasks.Add(_apiService.GetMonthlyData(firstDayOfMonth.ToString("yyyy-MM-dd"), lastDayOfMonth.ToString("yyyy-MM-dd"), latitude, longitude));

            // Loop for going back year by year
            for (var i = 1; i <= 20; i++)
            {
                var yearToSubtract = desiredDate.Year - i;
                var firstDayOfYear = new DateTime(yearToSubtract, desiredDate.Month, 1);
                var lastDayOfYear = new DateTime(yearToSubtract, desiredDate.Month + 1, 1);
                tasks.Add(_apiService.GetMonthlyData(firstDayOfYear.ToString("yyyy-MM-dd"), lastDayOfYear.ToString("yyyy-MM-dd"), latitude, longitude));
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
            .SelectMany(response => response?.weather)
            .Where(weather => weather?.TimeStamp.Day == day)
            .GroupBy(weather => weather?.TimeStamp)
            .Select(group => group.First())
            .ToList();

        mergedResponse.weather = weatherList.ToArray();

        return mergedResponse;
    }

    private static List<WeatherDataModel> CalculateDailyMeans(MultipleWeatherResponseModel mergedResponse)
    {
        var groupedByDay = mergedResponse.weather
            .GroupBy(w => w.TimeStamp.Date);

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
