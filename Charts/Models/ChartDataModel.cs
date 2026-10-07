namespace Server.Models;

public sealed class ChartDataModel
{
    public string X { get; init; } = string.Empty;
    public double High { get; init; }
    public double Low { get; init; }
    public double Mean { get; init; }
    public double Precipitation { get; init; }
}
