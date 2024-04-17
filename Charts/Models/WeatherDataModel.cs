using Microsoft.AspNetCore.Components;

namespace Server.Models;

public class WeatherDataModel
{
    public int Day { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public double MeanTemp { get; set; }
    public double MaxTemp { get; set; }
    public double MonthlyHigh { get; set; }
    public double MonthlyLow { get; set; }
    public double MinTemp { get; set; }
    public double Precipitation { get; set; }
    public double SunshineHours { get; set; }
    public double RecordHigh { get; set; }
    public double RecordLow { get; set; }
    public int RecordLowYear { get; set; }
    public int RecordHighYear { get; set; }
}
