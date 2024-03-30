using Charts.Models;

namespace Charts;

public class WeatherService(string latitude, string longitude)
{
    private readonly ApiService _apiService = new();
    private readonly string _latitude = latitude;
    private readonly string _longitude = longitude;

    public async Task<IReadOnlyCollection<DayModel>> GetWeatherDataForDisplay(DateTime desiredDate)
    {
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

            tasks.Add(_apiService.GetMonthlyData(firstDayOfMonth.ToString("yyyy-MM-dd"), lastDayOfMonth.ToString("yyyy-MM-dd"), _latitude, _longitude));

            // Loop for going back year by year
            for (var i = 1; i <= 20; i++)
            {
                var yearToSubtract = desiredDate.Year - i;
                var firstDayOfYear = new DateTime(yearToSubtract, desiredDate.Month, 1);
                var lastDayOfYear = new DateTime(yearToSubtract, desiredDate.Month + 1, 1);
                tasks.Add(_apiService.GetMonthlyData(firstDayOfYear.ToString("yyyy-MM-dd"), lastDayOfYear.ToString("yyyy-MM-dd"), _latitude, _longitude));
            }
        }
        else
        {
            for (var i = 0; i < 20; i++)
            {
                var dateString = desiredDate.AddYears(-i).ToString("yyyy-MM-dd");
                tasks.Add(_apiService.GetHistoricalWeatherData(dateString, _latitude, _longitude));
            }
        }

        return await Task.WhenAll(tasks);
    }

    private MultipleWeatherResponseModel MergeWeatherResponses(IEnumerable<MultipleWeatherResponseModel?> responses, int day)
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

    private static List<DayModel> CalculateDailyMeans(MultipleWeatherResponseModel mergedResponse)
    {
        var groupedByDay = mergedResponse.weather
            .GroupBy(w => w.TimeStamp.Date);

        return groupedByDay.Select(group => new DayModel
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
