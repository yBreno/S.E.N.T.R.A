namespace Application.Predictive;

/// <summary>Parametros do motor de regras (secao "Predictive" do appsettings). Tudo configuravel e auditavel.</summary>
public class PredictiveSettings
{
    public const string SectionName = "Predictive";

    public RiskWeights Weights { get; set; } = new();
    /// <summary>Janela da densidade de ocorrencias (dias).</summary>
    public int DensityWindowDays { get; set; } = 7;
    /// <summary>Janela das falhas de equipamentos (dias).</summary>
    public int FailureWindowDays { get; set; } = 30;
    /// <summary>Falhas na janela a partir das quais o equipamento e considerado "recorrente".</summary>
    public int RecurringFailureMinCount { get; set; } = 2;

    public double MediumThreshold { get; set; } = 25;
    public double HighThreshold { get; set; } = 50;
    public double CriticalThreshold { get; set; } = 75;

    /// <summary>Janelas de horario de pico (hora local, [Start, End)).</summary>
    public List<PeakWindow> PeakWindows { get; set; } = [new PeakWindow { Start = 7, End = 9 }, new PeakWindow { Start = 17, End = 19 }];
    /// <summary>Fuso do operador em relacao ao UTC (Brasilia = -3).</summary>
    public int UtcOffsetHours { get; set; } = -3;

    // Anomalias
    public int AnomalyWindowHours { get; set; } = 3;
    public int AnomalyBaselineWeeks { get; set; } = 6;
    public double AnomalySigma { get; set; } = 2;
    public int AnomalyMinCount { get; set; } = 3;
    /// <summary>Falhas repetidas no mesmo equipamento (dentro de FailureWindowDays) que caracterizam anomalia.</summary>
    public int RepeatedFailureCount { get; set; } = 3;

    // Previsao de falha (MTBF)
    public double MtbfWarningRatio { get; set; } = 0.85;
    public int PreventiveScheduleDays { get; set; } = 2;
}

public class RiskWeights
{
    public double Density { get; set; } = 0.40;
    public double Failures { get; set; } = 0.30;
    public double Peak { get; set; } = 0.20;
    public double Unavailability { get; set; } = 0.10;
}

public class PeakWindow
{
    public int Start { get; set; }
    public int End { get; set; }
}
