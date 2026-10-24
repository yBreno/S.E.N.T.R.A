using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Predictive;
using Domain.Entities;
using Domain.Enums;
namespace Infrastructure.Demo;

/// <summary>Perfil de avaliação do carregador existente; somente dados sintéticos e regras atuais.</summary>
public static class DemoEvaluationPlan
{
    public const string Prefix = "DEMO-S07";
    public const string DatabasePrefix = "SENTRA_DEMO_Avaliacao_";
    public static Guid Id(string kind, int index) => new(SHA256.HashData(Encoding.UTF8.GetBytes($"{Prefix}/{kind}/{index}"))[..16]);
    public static Guid PreventiveRecommendationId => Id("recommendation", 0);
    public static string SyntheticDocument(int index) => index is >= 0 and <= 9 ? new string((char)('0' + index), 11) : throw new ArgumentOutOfRangeException(nameof(index));
    public static bool IsSyntheticDocument(string value) => value.Length == 11 && value.All(char.IsAsciiDigit) && value.Distinct().Count() == 1;

    public static DemoDataPlan Create(DateTime at)
    {
        if (at.Kind != DateTimeKind.Utc) throw new ArgumentException("Referência deve estar em UTC.");
        (string Name, double Lat, double Lon)[] places = [("Centro", -23.5505, -46.6333), ("Bela Vista", -23.5614, -46.6559),
            ("Pinheiros", -23.5617, -46.6820), ("Vila Mariana", -23.5891, -46.6347), ("Mooca", -23.5558, -46.5969)];
        int[] regions = [0, 0, 4, 3, 2, 1, 0, 2, 4, 1, 0, 4];
        string[] names = ["C-PREV", "B-RADAR", "E-BALANCA", "A-ESCOLAR", "A-PATINETE", "E-TOTEM", "A-MOTO", "A-AUTONOMO", "A-CANCELA", "A-TAXI", "B-CAMERA", "E-HISTORICO"];
        Equipment[] equipments = [new Camera { HasNightVision = true, IsColor = true, Resolution = "DEMO 1080p" }, new Radar { RadarType = "DEMO velocidade", MaxSpeedLimit = 50 },
            new Sensor { SensorType = "DEMO pesagem", MeasurementUnit = "kg" }, new Vehicle(), new ElectricalScooter { BatteryCapacity = "DEMO 400 Wh", MaxSpeed = 20 },
            new ParkingMeter { Spots = 8 }, new Motorcycle { EngineCapacityCc = 160 }, new AutonomousVehicle { AutonomyLevel = 3 },
            new Sensor { SensorType = "DEMO controlador de acesso", MeasurementUnit = "ciclos" }, new Vehicle(), new Camera { Resolution = "DEMO 720p" }, new Camera { HasNightVision = true, Resolution = "DEMO 1080p" }];
        string[] categories = ["Videomonitoramento DEMO", "Fiscalização DEMO", "Balança de caminhão DEMO", "Transporte escolar DEMO", "Patinete compartilhado DEMO", "Estacionamento DEMO", "Moto de agente DEMO", "Fiscalização autônoma DEMO", "Controlador IoT de cancela DEMO", "Táxi DEMO", "Videomonitoramento DEMO", "Videomonitoramento DEMO"];
        var addresses = new List<Address>();
        for (var i = 0; i < equipments.Length; i++)
        {
            var n = places[regions[i]];
            var address = new Address { Id = Id("address", i), CreatedAt = at.AddDays(-90), Street = $"Via fictícia DEMO {i + 1:00}", Number = $"{100 + i * 10}",
                Complement = "Instalação ilustrativa", Neighborhood = n.Name, City = "São Paulo", State = States.SP, ZipCode = "00000000", Country = "Brasil",
                LocationReference = $"{Prefix}/address/{i} — coordenadas ilustrativas; sem instalação real", Latitude = n.Lat + (i % 4 - 1.5) * .0012, Longitude = n.Lon + (i / 4 - 1) * .0014 };
            addresses.Add(address);
            var e = equipments[i]; e.Id = Id("equipment", i); e.CreatedAt = at.AddDays(-90); e.SerialNumber = $"{Prefix}-{names[i]}";
            e.Brand = "DEMO"; e.Model = $"Modelo fictício {i:00}"; e.Category = categories[i]; e.AddressId = address.Id;
            if (e is Vehicle vehicle) { vehicle.LicensePlate = $"DMO{i:0000}"; vehicle.ManufactureYear = 2024; vehicle.Chassi = $"DEMO{i:0000000000000}"; }
            if (i != 8) e.ChangeStatus(EquipmentStatus.Operational);
        }
        equipments[1].ChangeStatus(EquipmentStatus.Malfunctioning); equipments[1].ChangeStatus(EquipmentStatus.UnderMaintenance); equipments[1].MaintenanceNeed = MaintenanceNeed.Immediate;
        equipments[5].ChangeStatus(EquipmentStatus.UnderMaintenance); equipments[5].MaintenanceNeed = MaintenanceNeed.Scheduled; equipments[5].NextMaintenanceDate = at.AddHours(-1);
        equipments[7].ChangeStatus(EquipmentStatus.Deactivated);
        equipments[10].ChangeStatus(EquipmentStatus.Malfunctioning); equipments[10].MaintenanceNeed = MaintenanceNeed.Immediate; equipments[10].NextMaintenanceDate = at.AddHours(6);
        equipments[0].MaintenanceNeed = MaintenanceNeed.Periodic; equipments[0].NextMaintenanceDate = at.AddDays(-1);
        equipments[2].MaintenanceNeed = MaintenanceNeed.Scheduled; equipments[2].NextMaintenanceDate = at.AddDays(2);
        equipments[11].MaintenanceNeed = MaintenanceNeed.Periodic; equipments[11].NextMaintenanceDate = at.AddDays(15);

        AgentAvailability[] availability = [AgentAvailability.Busy, AgentAvailability.Offline, AgentAvailability.Available, AgentAvailability.Busy, AgentAvailability.Available, AgentAvailability.Available, AgentAvailability.Offline, AgentAvailability.Available, AgentAvailability.Busy];
        int[] agentRegions = [0, 0, 1, 2, 2, 3, 4, 4, 1];
        var agents = Enumerable.Range(0, 9).Select(i => new Agent { Id = Id("agent", i), CreatedAt = at.AddDays(-60), Name = $"DEMO Profissional {i + 1:00}",
            Cpf = SyntheticDocument(i), BadgeNumber = $"{Prefix}-AG-{i:00}", Role = (AgentRole)(i % 3), Availability = availability[i],
            Neighborhood = places[agentRegions[i]].Name, Latitude = places[agentRegions[i]].Lat + (i + 1) * .00015,
            Longitude = places[agentRegions[i]].Lon - (i + 1) * .00012, LocationUpdatedAt = at.AddMinutes(-i - 1) }).ToList();
        ((Vehicle)equipments[3]).CurrentAgentId = agents[5].Id;
        ((Vehicle)equipments[6]).CurrentAgentId = agents[0].Id;
        ((Vehicle)equipments[9]).CurrentAgentId = agents[2].Id;

        var incidents = new List<Incident>(); var alerts = new List<Alert>(); var orders = new List<MaintenanceRecord>(); var telemetry = new List<TelemetryLog>();
        void Failure(int index, int equipment, DateTime when, DateTime? restored)
        {
            var a = addresses[equipment];
            incidents.Add(new() { Id = Id("incident", index), CreatedAt = when, OccurredAt = when, Type = IncidentType.EquipmentFailure, Severity = IncidentSeverity.High,
                Neighborhood = a.Neighborhood, Latitude = a.Latitude, Longitude = a.Longitude, EquipmentId = equipments[equipment].Id, Description = $"{Prefix}/incident/{index} — falha sintética ilustrativa" });
            alerts.Add(new() { Id = Id("alert", index), CreatedAt = when, EquipmentId = equipments[equipment].Id, Type = AlertType.Failure, Severity = restored.HasValue ? AlertSeverity.High : AlertSeverity.Critical,
                Status = restored.HasValue ? AlertStatus.Resolved : AlertStatus.Open, ResolvedAt = restored, Message = $"{Prefix}/alert/{index} — falha DEMO em {equipments[equipment].SerialNumber}" });
        }
        int[] days = [20, 14, 6];
        for (var i = 0; i < days.Length; i++)
        {
            var start = at.AddDays(-days[i]); var end = start.AddDays(1); Failure(i, 0, start, end);
            orders.Add(new() { Id = Id("maintenance", i), CreatedAt = start, EquipmentId = equipments[0].Id, Kind = MaintenanceKind.Corrective, Status = MaintenanceStatus.Closed,
                InitDate = start, EndDate = end, Cost = 100 + i * 25, PeriodicityDays = i == 2 ? 4 : null, Urgency = MaintenanceUrgency.High, Description = $"{Prefix}/maintenance/{i} — corretiva histórica DEMO" });
            telemetry.Add(new() { Id = Id("telemetry", i), CreatedAt = at.AddHours(-i - 1), Timestamp = at.AddHours(-i - 1), EquipmentId = equipments[0].Id,
                EventType = i == 1 ? "Temperatura Elevada" : "Bateria Fraca", PayloadData = $"{{\"synthetic\":true,\"dataset\":\"{Prefix}\",\"scenario\":\"C\"}}" });
        }
        Failure(3, 1, at.AddDays(-8), at.AddDays(-7)); Failure(4, 1, at.AddHours(-6), null); Failure(5, 10, at.AddHours(-2), null);
        orders.Add(new() { Id = Id("maintenance", 3), CreatedAt = at.AddDays(-8), EquipmentId = equipments[1].Id, Kind = MaintenanceKind.Corrective, Status = MaintenanceStatus.Closed,
            InitDate = at.AddDays(-8), EndDate = at.AddDays(-7), Cost = 200, Urgency = MaintenanceUrgency.High, Description = $"{Prefix}/maintenance/3 — reparo anterior DEMO" });
        orders.Add(new() { Id = Id("maintenance", 4), CreatedAt = at.AddHours(-4), EquipmentId = equipments[1].Id, AgentId = agents[0].Id, Kind = MaintenanceKind.Corrective, Status = MaintenanceStatus.Open,
            InitDate = at.AddHours(-4), Urgency = MaintenanceUrgency.Critical, Description = $"{Prefix}/maintenance/4 — resposta à falha DEMO do radar" });
        orders.Add(new() { Id = Id("maintenance", 5), CreatedAt = at.AddHours(-1), EquipmentId = equipments[10].Id, Kind = MaintenanceKind.Corrective, Status = MaintenanceStatus.Scheduled,
            ScheduledDate = at.AddHours(6), Urgency = MaintenanceUrgency.High, Description = $"{Prefix}/maintenance/5 — corretiva programada DEMO" });
        orders.Add(new() { Id = Id("maintenance", 6), CreatedAt = at.AddDays(-1), EquipmentId = equipments[2].Id, AgentId = agents[7].Id, Kind = MaintenanceKind.Preventive, Status = MaintenanceStatus.Scheduled,
            ScheduledDate = at.AddDays(2), PeriodicityDays = 30, Urgency = MaintenanceUrgency.Medium, Description = $"{Prefix}/maintenance/6 — inspeção preventiva DEMO da balança" });
        orders.Add(new() { Id = Id("maintenance", 7), CreatedAt = at.AddDays(-1), EquipmentId = equipments[5].Id, Kind = MaintenanceKind.Preventive, Status = MaintenanceStatus.Open,
            ScheduledDate = at.AddHours(-1), InitDate = at.AddHours(-2), Urgency = MaintenanceUrgency.Medium, Description = $"{Prefix}/maintenance/7 — preventiva DEMO iniciada antecipadamente" });
        orders.Add(new() { Id = Id("maintenance", 8), CreatedAt = at.AddDays(-16), EquipmentId = equipments[11].Id, Kind = MaintenanceKind.Preventive, Status = MaintenanceStatus.Closed,
            InitDate = at.AddDays(-16), EndDate = at.AddDays(-15), PeriodicityDays = 30, Cost = 150, Urgency = MaintenanceUrgency.Low, Description = $"{Prefix}/maintenance/8 — preventiva DEMO concluída" });
        for (var i = 0; i < 13; i++)
        {
            var place = places[i < 10 ? 0 : new[] { 1, 2, 4 }[i - 10]]; var when = at.AddHours(-i * 3 - 1);
            incidents.Add(new() { Id = Id("incident", i + 6), CreatedAt = when, OccurredAt = when, Type = i % 2 == 0 ? IncidentType.Congestion : IncidentType.TrafficAccident,
                Severity = i < 10 ? IncidentSeverity.High : IncidentSeverity.Low, Neighborhood = place.Name, Latitude = place.Lat + (i % 3 - 1) * .0004,
                Longitude = place.Lon + (i % 4 - 1.5) * .0004, Description = $"{Prefix}/incident/{i + 6} — ocorrência sintética de tráfego; sem fonte oficial" });
        }
        foreach (var i in new[] { 2, 5 }) alerts.Add(new() { Id = Id("alert", i + 10), CreatedAt = at.AddDays(-1), EquipmentId = equipments[i].Id,
            Type = AlertType.Preventive, Severity = AlertSeverity.Medium, Status = AlertStatus.Open, Message = $"{Prefix}/alert/{i + 10} — inspeção DEMO planejada; não indica falha" });
        foreach (var i in new[] { 1, 10, 2, 3, 11 }) telemetry.Add(new() { Id = Id("telemetry", i + 10), CreatedAt = at.AddMinutes(-30 - i), Timestamp = at.AddMinutes(-30 - i), EquipmentId = equipments[i].Id,
            EventType = i is 1 or 10 ? "ConnectionLost" : "Heartbeat", PayloadData = $"{{\"synthetic\":true,\"dataset\":\"{Prefix}\",\"source\":\"fixture\"}}" });

        // As explicações são calculadas pelos mesmos motores da aplicação, sem executar seeder ou worker.
        foreach (var e in equipments) e.Address = addresses.Single(a => a.Id == e.AddressId);
        var assessment = EquipmentRiskCalculator.Calculate(at, equipments, alerts, orders, telemetry).Single(r => r.EquipmentId == equipments[0].Id);
        var regional = RiskCalculator.Calculate(new(at, incidents, alerts, equipments, agents), new()).Single(r => r.Neighborhood == "Centro");
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
        Recommendation[] recommendations = [new() { Id = PreventiveRecommendationId, CreatedAt = at, Type = RecommendationType.SendMaintenance, Status = RecommendationStatus.Pending,
            EquipmentId = equipments[0].Id, Neighborhood = "Centro", RiskScore = assessment.Score, Reason = $"{Prefix} C — priorizar inspeção preventiva da câmera operacional com falhas históricas restauradas.",
            ExplanationJson = JsonSerializer.Serialize(new { synthetic = true, dataset = Prefix, referenceUtc = at, trigger = "EquipmentRisk", priority = "High", action = assessment.RecommendedAction, assessment }, json) },
            new() { Id = Id("recommendation", 1), CreatedAt = at, Type = RecommendationType.DeployAgent, Status = RecommendationStatus.Pending, Neighborhood = "Centro", RiskScore = regional.Score,
                Reason = $"{Prefix} D — avaliar reforço no Centro: concentração demonstrativa de ocorrências e ausência de agentes disponíveis.",
                ExplanationJson = JsonSerializer.Serialize(new { synthetic = true, dataset = Prefix, referenceUtc = at, trigger = "RiskLevel", rule = "Índice regional alto/crítico calculado sobre a fixture DEMO", risk = regional }, json) }];
        foreach (var e in equipments) e.Address = null!;
        var plan = new DemoDataPlan(addresses, equipments, agents, incidents, alerts, orders) { Dataset = Prefix, ReferenceUtc = at, Telemetry = telemetry, Recommendations = recommendations };
        plan.Validate(); return plan;
    }
}
