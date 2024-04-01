namespace Server.Models;

public class DayModel
{
    public int Day { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public double MeanTemp { get; set; }
    public double MaxTemp { get; set; }
    public double MinTemp { get; set; }
    public double Precipitation { get; set; }
    public double SunshineHours { get; set; }
}
