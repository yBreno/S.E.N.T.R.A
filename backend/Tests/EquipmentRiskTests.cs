using Application.Predictive;
using Domain.Entities;
using Domain.Enums;
namespace Tests;

public class EquipmentRiskTests
{
    private static readonly DateTime Agora = new(2026, 10, 24, 15, 0, 0, DateTimeKind.Utc);
    internal static Camera Equipamento()
    {
        var camera = new Camera { SerialNumber = "PRED-TESTE", Address = new Address { Neighborhood = "Centro" }, CreatedAt = Agora.AddDays(-90) };
        camera.AddressId = camera.Address.Id;
        camera.ChangeStatus(EquipmentStatus.Operational);
        return camera;
    }
    internal static List<Alert> Falhas(Guid id, DateTime at, int count = 3) => Enumerable.Range(1, count)
        .Select(i => new Alert { EquipmentId = id, Type = AlertType.Failure, CreatedAt = at.AddDays(-10 + i) }).ToList();

    [Theory]
    [InlineData(0, false, 0, RiskLevel.Low)]
    [InlineData(3, false, 30, RiskLevel.Medium)]
    [InlineData(3, true, 55, RiskLevel.High)]
    public void Classifica_risco_baixo_medio_e_alto_com_pontuacao_explicavel(int failures, bool overdue, double expected, RiskLevel level)
    {
        var e = Equipamento();
        if (overdue) { e.MaintenanceNeed = MaintenanceNeed.Periodic; e.NextMaintenanceDate = Agora.AddDays(-1); }
        var result = Assert.Single(EquipmentRiskCalculator.Calculate(Agora, [e], Falhas(e.Id, Agora, failures), [], []));
        Assert.Equal(expected, result.Score); Assert.Equal(level, result.Level);
        Assert.Equal(result.Score, Math.Round(result.Factors.Sum(f => f.Points), 1));
        Assert.Equal(1, result.Factors.Sum(f => f.Weight), 8);
        Assert.All(result.Factors, f => { Assert.NotEmpty(f.Rule); Assert.InRange(f.Normalized, 0, 1); });
        Assert.Equal(DateTimeKind.Utc, result.EvaluatedAt.Kind); Assert.Equal(Agora.AddDays(-30), result.WindowFrom);
    }

    [Fact]
    public void Historico_insuficiente_e_explicito_sem_probabilidade_inventada()
    {
        var e = Equipamento();
        var result = Assert.Single(EquipmentRiskCalculator.Calculate(Agora, [e], [], [], []));
        Assert.False(result.HasSufficientHistory); Assert.Contains("insuficiente", result.HistoryDescription);
        Assert.Contains("ausência", result.Summary); Assert.Null(result.RecommendedAction);
    }

    [Fact]
    public void Falha_atual_recomenda_acao_mesmo_sem_historico_para_estatistica()
    {
        var e = Equipamento(); e.ChangeStatus(EquipmentStatus.Malfunctioning);
        var result = Assert.Single(EquipmentRiskCalculator.Calculate(Agora, [e], [], [], []));
        Assert.Equal(15, result.Score); Assert.False(result.HasSufficientHistory);
        Assert.Contains("falha atual", result.RecommendedAction);
    }

    [Fact]
    public void Corretivas_recorrentes_e_telemetria_compartilham_janela_e_limitam_indice_a_cem()
    {
        var e = Equipamento(); e.ChangeStatus(EquipmentStatus.Malfunctioning);
        var orders = Enumerable.Range(0, 4).Select(i => new MaintenanceRecord { EquipmentId = e.Id, Kind = MaintenanceKind.Corrective, InitDate = Agora.AddDays(-i), Status = MaintenanceStatus.Closed }).ToList();
        orders.Add(new() { EquipmentId = e.Id, Kind = MaintenanceKind.Preventive, Status = MaintenanceStatus.Scheduled, ScheduledDate = Agora.AddDays(-1) });
        var signals = Enumerable.Range(0, 4).Select(i => new TelemetryLog { EquipmentId = e.Id, Timestamp = Agora.AddDays(-i), EventType = "LowBattery", PayloadData = "nao deve ser exposto" }).ToList();
        var result = Assert.Single(EquipmentRiskCalculator.Calculate(Agora, [e], Falhas(e.Id, Agora, 4), orders, signals));
        Assert.Equal(100, result.Score); Assert.Equal(RiskLevel.High, result.Level);
        Assert.Contains(result.Factors, f => f.Name == "CorrectiveFrequency" && f.Points == 20 && f.RecordCount == 4);
        Assert.Contains(result.Factors, f => f.Name == "TelemetrySignals" && f.Points == 10 && f.RecordCount == 4);
        Assert.DoesNotContain("nao deve ser exposto", System.Text.Json.JsonSerializer.Serialize(result));
    }

    [Fact]
    public void Preventiva_atrasada_usa_ordem_real_sem_exigir_planejamento_duplicado_no_inventario()
    {
        var e = Equipamento();
        var order = new MaintenanceRecord { EquipmentId = e.Id, Kind = MaintenanceKind.Preventive, Status = MaintenanceStatus.Scheduled, ScheduledDate = Agora.AddDays(-3) };
        var result = Assert.Single(EquipmentRiskCalculator.Calculate(Agora, [e], [], [order], []));
        Assert.Equal(25, result.Score); Assert.NotNull(result.RecommendedAction);
        Assert.Contains(result.Factors, f => f.Name == "PreventivePending" && f.Points == 25 && f.RecordIds.Contains(order.Id));
    }

    [Fact]
    public void Ignora_eventos_futuros_antigos_heartbeat_e_ordens_sem_inicio_real()
    {
        var e = Equipamento();
        var failures = new List<Alert> { new() { EquipmentId = e.Id, Type = AlertType.Failure, CreatedAt = Agora.AddDays(1) }, new() { EquipmentId = e.Id, Type = AlertType.Failure, CreatedAt = Agora.AddDays(-31) } };
        var result = Assert.Single(EquipmentRiskCalculator.Calculate(Agora, [e], failures,
            [new() { EquipmentId = e.Id, Kind = MaintenanceKind.Corrective, Status = MaintenanceStatus.Scheduled }],
            [new() { EquipmentId = e.Id, Timestamp = Agora, EventType = "Heartbeat" }, new() { EquipmentId = e.Id, Timestamp = Agora.AddDays(1), EventType = "LowBattery" }]));
        Assert.Equal(0, result.Score); Assert.False(result.HasSufficientHistory);
    }

    [Fact]
    public void Composicao_por_bairro_inclui_cobertura_sem_expor_identificacao_sensivel()
    {
        var e = Equipamento(); e.ChangeStatus(EquipmentStatus.Malfunctioning);
        var agent = new Agent { Neighborhood = "Centro", Availability = AgentAvailability.Available, Cpf = "sensivel" };
        var incidents = new List<Incident> { new() { Neighborhood = "Centro", OccurredAt = Agora.AddHours(-1) } };
        var settings = new PredictiveSettings();
        var available = Assert.Single(RiskCalculator.Calculate(new(Agora, incidents, Falhas(e.Id, Agora), [e], [agent]), settings));
        var uncovered = Assert.Single(RiskCalculator.Calculate(new(Agora, incidents, Falhas(e.Id, Agora), [e], []), settings));
        Assert.Equal(15, uncovered.Score - available.Score, 1);
        Assert.Contains(available.Factors, f => f.Name == "AgentCoverageGap" && f.Points == 0 && f.RecordIds.Contains(agent.Id));
        Assert.Equal(Agora.AddDays(-7), available.IncidentWindowFrom); Assert.Equal(Agora.AddDays(-30), available.FailureWindowFrom);
        Assert.Equal(1, available.Factors.Sum(f => f.Weight), 8);
        Assert.DoesNotContain("sensivel", System.Text.Json.JsonSerializer.Serialize(available));
    }

    [Fact]
    public void Mtbf_exclui_falhas_futuras_e_intervalos_nulos_sem_criar_uma_estimativa()
    {
        var e = Equipamento();
        var failures = new List<Alert> { new() { EquipmentId = e.Id, CreatedAt = Agora.AddDays(-1) }, new() { EquipmentId = e.Id, CreatedAt = Agora.AddDays(-1) }, new() { EquipmentId = e.Id, CreatedAt = Agora.AddDays(3) } };
        Assert.Empty(FailurePredictor.Predict(Agora, [e], failures, [], new()));
    }
}
