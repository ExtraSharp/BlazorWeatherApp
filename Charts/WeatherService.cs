using Server.Models;

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
                // Include other properties as needed
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
