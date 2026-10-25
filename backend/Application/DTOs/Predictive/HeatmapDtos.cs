using Domain.Enums;
namespace Application.DTOs.Predictive;

public enum HeatmapMetric
{
    Incidents,
    Failures,
    Risk
}

public record HeatmapQueryDto
{
    public HeatmapMetric Metric { get; init; } = HeatmapMetric.Incidents;
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public int? FromHour { get; init; }
    public int? ToHour { get; init; }
    public DayOfWeek? DayOfWeek { get; init; }
    public string? Neighborhood { get; init; }
    public IncidentType? Type { get; init; }
}

public record HeatPointDto(double Latitude, double Longitude, double Intensity, string Neighborhood, string? Label);

public record HeatmapDto(HeatmapMetric Metric, DateTime From, DateTime To, int TotalPoints, double MaxIntensity, List<HeatPointDto> Points);
