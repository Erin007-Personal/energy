namespace PowerpalTray;

internal sealed class PowerpalReading
{
    public double? Kilowatts { get; init; }
    public bool HasNoReadingYet { get; init; }
    public string? Error { get; init; }
}
