using Application.DTOs.Predictive;
using Domain.Entities;
using Domain.Enums;
namespace Application.Predictive;

public static class EquipmentRiskCalculator
{
    // Pesos fixos somam 100 pontos. Eventos conhecidos são sinais categóricos, não anomalias estatísticas.
    private static readonly HashSet<string> AttentionEvents = new(StringComparer.OrdinalIgnoreCase)
    { "ConnectionLost", "ConexaoPerdida", "Conexao Perdida", "Conexão Perdida", "LowBattery", "Bateria Fraca", "HighTemperature", "Temperatura Elevada" };

    public static List<EquipmentRiskDto> Calculate(DateTime at, IReadOnlyList<Equipment> equipments,
        IReadOnlyList<Alert> failures, IReadOnlyList<MaintenanceRecord> maintenances,
        IReadOnlyList<TelemetryLog> telemetry, int windowDays = 30)
    {
        at = DateTime.SpecifyKind(at, DateTimeKind.Utc);
        var from = at.AddDays(-windowDays);
        var failuresByEquipment = failures.Where(a => a.Type == AlertType.Failure && a.CreatedAt >= from && a.CreatedAt <= at).ToLookup(a => a.EquipmentId);
        var maintenanceByEquipment = maintenances.ToLookup(m => m.EquipmentId);
        var signalsByEquipment = telemetry.Where(t => t.Timestamp >= from && t.Timestamp <= at && AttentionEvents.Contains(t.EventType.Trim())).ToLookup(t => t.EquipmentId);
        var result = new List<EquipmentRiskDto>();
        foreach (var equipment in equipments)
        {
            var history = failuresByEquipment[equipment.Id].ToList();
            var orders = maintenanceByEquipment[equipment.Id].ToList();
            var corrective = orders.Where(m => m.Kind == MaintenanceKind.Corrective && m.InitDate >= from && m.InitDate <= at).ToList();
            var preventive = orders.Where(m => m.Kind == MaintenanceKind.Preventive && m.Status == MaintenanceStatus.Scheduled).ToList();
            var dates = preventive.Where(m => m.ScheduledDate.HasValue).Select(m => m.ScheduledDate!.Value).ToList();
            if (equipment.MaintenanceNeed is MaintenanceNeed.Scheduled or MaintenanceNeed.Periodic && equipment.NextMaintenanceDate.HasValue)
                dates.Add(equipment.NextMaintenanceDate.Value);
            var overdue = dates.Any(d => d < at);
            var dueSoon = dates.Any(d => d <= at.AddDays(7));
            var pending = preventive.Count > 0 || equipment.MaintenanceNeed is MaintenanceNeed.Scheduled or MaintenanceNeed.Periodic;
            var preventiveNorm = overdue ? 1d : dueSoon ? .4 : pending ? .2 : 0;
            var unavailable = equipment.Status is EquipmentStatus.Malfunctioning or EquipmentStatus.UnderMaintenance or EquipmentStatus.Stolen;
            var signals = signalsByEquipment[equipment.Id].ToList();
            static RiskFactorDto Factor(string name, string label, double raw, string description, double norm, double weight,
                string rule, string kind, IEnumerable<Guid> evidence)
            {
                var ids = evidence.Distinct().ToList();
                return new(name, label, raw, description, Math.Round(norm, 4), weight, Math.Round(norm * weight * 100, 2), rule, kind, ids.Count, ids.Take(50).ToList());
            }
            var factors = new List<RiskFactorDto>
            {
                Factor("RecentFailures", "Falhas recentes", history.Count, $"{history.Count} alerta(s) de falha em {windowDays} dias.",
                    Math.Min(history.Count / 3d, 1), .30, "mínimo(contagem / 3, 1) × 30 pontos", "Alert", history.Select(a => a.Id)),
                Factor("CorrectiveFrequency", "Frequência de corretivas", corrective.Count, $"{corrective.Count} corretiva(s) efetivamente iniciada(s) em {windowDays} dias.",
                    Math.Min(corrective.Count / 3d, 1), .20, "mínimo(contagem / 3, 1) × 20 pontos", "MaintenanceRecord", corrective.Select(m => m.Id)),
                Factor("PreventivePending", "Preventiva pendente ou atrasada", dates.Count,
                    overdue ? "Data preventiva vencida, em ordem agendada ou no planejamento do inventário." : dueSoon ? "Previsão preventiva nos próximos 7 dias." : pending ? "Preventiva pendente; confira o planejamento e a existência de ordem." : "Nenhuma preventiva pendente registrada.",
                    preventiveNorm, .25, "atrasada: 25; até 7 dias: 10; outra pendência: 5; sem pendência: 0 pontos. Planejamento não comprova execução.", "MaintenanceRecord/Equipment", preventive.Select(m => m.Id).Concat(pending ? [equipment.Id] : [])),
                Factor("OperationalUnavailability", "Indisponibilidade operacional", unavailable ? 1 : 0, $"Situação atual: {equipment.Status}.",
                    unavailable ? 1 : 0, .15, "falha, manutenção ou furto: 15; demais situações: 0 pontos", "Equipment", [equipment.Id]),
                Factor("TelemetrySignals", "Sinais de atenção registrados", signals.Count, $"{signals.Count} evento(s) categórico(s) de conexão, bateria ou temperatura em {windowDays} dias; não comprova anomalia estatística.",
                    Math.Min(signals.Count / 3d, 1), .10, "mínimo(contagem de eventos conhecidos / 3, 1) × 10 pontos; payload não é interpretado", "TelemetryLog", signals.Select(t => t.Id))
            };
            var score = Math.Clamp(Math.Round(factors.Sum(f => f.Points), 1), 0, 100);
            var level = score >= 50 ? RiskLevel.High : score >= 25 ? RiskLevel.Medium : RiskLevel.Low;
            var sufficient = history.Select(a => a.CreatedAt).Distinct().Count() >= 2;
            var top = factors.OrderByDescending(f => f.Points).First();
            string? action = equipment.Status == EquipmentStatus.Stolen ? "Verificar ocorrência de furto e recuperação do recurso antes de planejar manutenção." :
                equipment.Status == EquipmentStatus.UnderMaintenance ? "Acompanhar a intervenção existente e revisar o histórico, sem abrir outra ordem." :
                equipment.Status == EquipmentStatus.Malfunctioning ? "Atender a falha atual, revisar recorrência e consultar ordens existentes antes de abrir uma corretiva." :
                score >= 25 || overdue ? "Priorizar inspeção, verificar recorrência de falhas e revisar/agendar a manutenção conforme a situação e as ordens existentes." : null;
            result.Add(new(equipment.Id, equipment.SerialNumber, equipment.Type.ToString(), equipment.Status, equipment.Address?.Neighborhood ?? "",
                score, level, at, from, sufficient,
                sufficient ? "Há ao menos um intervalo entre falhas recentes; amostra descritiva, sem calibração probabilística." : "Histórico insuficiente: menos de duas falhas em instantes distintos na janela; risco baixo não garante ausência de falha.",
                score > 0 ? $"Índice {score:0.#}/100. Principal contribuição: {top.Label} ({top.Points:0.##} pontos)." : "Nenhum fator pontuou nos registros disponíveis; ausência de registros não comprova segurança.", action, factors));
        }
        return result.OrderByDescending(r => r.Score).ThenBy(r => r.SerialNumber).ToList();
    }
}
