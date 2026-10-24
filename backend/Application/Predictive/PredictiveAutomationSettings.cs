namespace Application.Predictive;

public class PredictiveAutomationSettings
{
    public const string SectionName = "PredictiveAutomation";
    public bool Enabled { get; set; } = false;
    public int IntervalMinutes { get; set; } = 60;
    public bool HasValidInterval => IntervalMinutes is >= 15 and <= 1440;
}
