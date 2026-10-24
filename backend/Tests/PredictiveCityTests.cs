using Application.DTOs.Equipment;
using Application.DTOs.Predictive;
using Application.Predictive;
using Application.Services;
using Application.Validators;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Tests;

public class PredictiveCityTests
{
    private static PredictiveSettings DefaultSettings() => new();

    [Fact]
    public void RiskCalculator_computes_weighted_transparent_score_and_factors()
    {
        var settings = DefaultSettings();
        // 11:00 UTC = 08:00 UTC-3 (dentro da janela de pico 7h-9h)
        var at = new DateTime(2026, 10, 19, 11, 0, 0, DateTimeKind.Utc);

        var addrPaulista = new Address
        {
            Street = "Av. Paulista", Neighborhood = "Bela Vista", City = "SP", State = States.SP,
            ZipCode = "01310200", Latitude = -23.56, Longitude = -46.65
        };
        var camBroken = new Camera { SerialNumber = "CAM-1", Brand = "A", Model = "B", AddressId = addrPaulista.Id, Address = addrPaulista };
        camBroken.ChangeStatus(EquipmentStatus.Operational);
        camBroken.ChangeStatus(EquipmentStatus.Malfunctioning); // indisponivel (1/1 = 100%)

        var incidents = new List<Incident>
        {
            new() { Type = IncidentType.TrafficAccident, Severity = IncidentSeverity.High, Neighborhood = "Bela Vista", Latitude = -23.56, Longitude = -46.65, OccurredAt = at.AddHours(-2) },
            new() { Type = IncidentType.EquipmentFailure, Severity = IncidentSeverity.Critical, Neighborhood = "Bela Vista", Latitude = -23.56, Longitude = -46.65, OccurredAt = at.AddHours(-1) }
        };

        var alerts = new List<Alert>
        {
            new() { EquipmentId = camBroken.Id, Type = AlertType.Failure, Severity = AlertSeverity.High, Message = "F1", CreatedAt = at.AddDays(-5) },
            new() { EquipmentId = camBroken.Id, Type = AlertType.Failure, Severity = AlertSeverity.High, Message = "F2", CreatedAt = at.AddDays(-1) }
        };

        var risks = RiskCalculator.Calculate(new RiskInput(at, incidents, alerts, [camBroken]), settings);
        var bv = Assert.Single(risks);

        // Todos os 4 fatores estao no maximo (1.0): 40 + 30 + 20 + 10 = 100 pts (Critical)
        Assert.Equal("Bela Vista", bv.Neighborhood);
        Assert.Equal(100.0, bv.Score);
        Assert.Equal(RiskLevel.Critical, bv.Level);
        Assert.Equal(4, bv.Factors.Count);
        Assert.Contains(bv.Factors, f => f.Name == "IncidentDensity" && f.Points == 40);
        Assert.Contains(bv.Factors, f => f.Name == "RecurringFailures" && f.Points == 30);
        Assert.Contains(bv.Factors, f => f.Name == "PeakHour" && f.Points == 20);
        Assert.Contains(bv.Factors, f => f.Name == "UnavailableEquipment" && f.Points == 10);
    }

    [Fact]
    public void AnomalyDetector_detects_volume_spike_and_repeated_equipment_failures()
    {
        var settings = DefaultSettings();
        var at = new DateTime(2026, 10, 19, 15, 0, 0, DateTimeKind.Utc);

        var incidents = new List<Incident>();
        // Baseline de 6 semanas no mesmo dia da semana e janela de 3h: 1 ocorrencia por semana
        for (var w = 1; w <= 6; w++)
        {
            incidents.Add(new Incident
            {
                Type = IncidentType.TrafficAccident, Neighborhood = "Bela Vista",
                Latitude = -23.56, Longitude = -46.65, OccurredAt = at.AddDays(-7 * w).AddMinutes(-30)
            });
        }
        // Janela atual (ultimas 3h): 5 ocorrencias -> Z-score bem acima de media + 2*sigma
        for (var i = 0; i < 5; i++)
        {
            incidents.Add(new Incident
            {
                Type = IncidentType.TrafficAccident, Neighborhood = "Bela Vista",
                Latitude = -23.56, Longitude = -46.65, OccurredAt = at.AddMinutes(-(10 * (i + 1)))
            });
        }

        var addr = new Address { Neighborhood = "Centro", Latitude = -23.55, Longitude = -46.63 };
        var radar = new Radar { SerialNumber = "RAD-99", AddressId = addr.Id, Address = addr };
        var alerts = new List<Alert>
        {
            new() { EquipmentId = radar.Id, Type = AlertType.Failure, CreatedAt = at.AddDays(-10) },
            new() { EquipmentId = radar.Id, Type = AlertType.Failure, CreatedAt = at.AddDays(-5) },
            new() { EquipmentId = radar.Id, Type = AlertType.Failure, CreatedAt = at.AddDays(-1) }
        };

        var anomalies = AnomalyDetector.Detect(at, incidents, alerts, [radar], settings);
        Assert.Equal(2, anomalies.Count);
        Assert.Contains(anomalies, a => a.Kind == "VolumeSpike" && a.Neighborhood == "Bela Vista" && a.Current == 5);
        Assert.Contains(anomalies, a => a.Kind == "RepeatedFailure" && a.EquipmentId == radar.Id && a.Current == 3);
    }

    [Fact]
    public void FailurePredictor_identifies_equipment_approaching_mtbf_limit()
    {
        var settings = DefaultSettings();
        var at = new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);
        var addr = new Address { Neighborhood = "Centro", Latitude = -23.55, Longitude = -46.63 };
        var cam = new Camera
        {
            SerialNumber = "CAM-MTBF", Brand = "Axis", Model = "Q60",
            AddressId = addr.Id, Address = addr, CreatedAt = at.AddDays(-60)
        };
        cam.ChangeStatus(EquipmentStatus.Operational);

        // Falhas nos dias -43, -28, -13 -> intervalos de 15 e 15 dias -> MTBF = 15 dias.
        // Ultima restauracao ha 13 dias -> 13/15 = 86.7% (>= 85% Warning)
        var alerts = new List<Alert>
        {
            new() { EquipmentId = cam.Id, Type = AlertType.Failure, CreatedAt = at.AddDays(-43) },
            new() { EquipmentId = cam.Id, Type = AlertType.Failure, CreatedAt = at.AddDays(-28) },
            new() { EquipmentId = cam.Id, Type = AlertType.Failure, CreatedAt = at.AddDays(-13) }
        };

        var predictions = FailurePredictor.Predict(at, [cam], alerts, [], settings);
        var pred = Assert.Single(predictions);
        Assert.Equal(cam.Id, pred.EquipmentId);
        Assert.Equal("Warning", pred.State);
        Assert.True(pred.Ratio >= settings.MtbfWarningRatio);
    }

    [Fact]
    public async Task Integrated_incident_flow_updates_equipment_recalculates_risk_and_creates_explainable_recommendation()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var opts = Options.Create(DefaultSettings());
        var clock = TimeProvider.System;

        var addrRepo = new AddressRepository(db);
        var eqRepo = new EquipmentRepository(db);
        var agentRepo = new AgentRepository(db);
        var alertRepo = new AlertRepository(db);
        var maintRepo = new MaintenanceRecordRepository(db);
        var incRepo = new IncidentRepository(db);
        var recRepo = new RecommendationRepository(db);

        var eqService = new EquipmentService(eqRepo, addrRepo, agentRepo, alertRepo, maintRepo, db,
            new CreateEquipmentDtoValidator(), new UpdateEquipmentDtoValidator(), new ChangeEquipmentStatusDtoValidator());
        var riskService = new RiskService(incRepo, alertRepo, eqRepo, opts, clock);
        var maintenance = new MaintenanceService(maintRepo, eqRepo, agentRepo, alertRepo, db,
            new OpenMaintenanceDtoValidator(), new CloseMaintenanceDtoValidator(), new ScheduleMaintenanceDtoValidator());
        using var coordination = new InProcessPredictiveWriteCoordinator();
        var recService = new RecommendationService(recRepo, agentRepo, eqRepo, db, opts, clock, maintenance, maintRepo, coordination);
        var incService = new IncidentService(incRepo, eqRepo, eqService, riskService, recService, db,
            new CreateIncidentDtoValidator(), clock);

        var addr = new Address
        {
            Street = "Av. Paulista", Neighborhood = "Bela Vista", City = "SP", State = States.SP,
            ZipCode = "01310200", Latitude = -23.56, Longitude = -46.65
        };
        db.Addresses.Add(addr);

        var cam = new Camera { SerialNumber = "CAM-INT-01", Brand = "Axis", Model = "M1", AddressId = addr.Id };
        cam.ChangeStatus(EquipmentStatus.Operational);
        db.Equipments.Add(cam);

        db.Agents.Add(new Agent
        {
            Name = "Agente Silva", Cpf = "52998224725", BadgeNumber = "B1",
            Role = AgentRole.TrafficAgent, Availability = AgentAvailability.Available,
            Neighborhood = "Bela Vista", Latitude = -23.561, Longitude = -46.651
        });
        await db.SaveChangesAsync();

        // Registra ocorrencia de falha de equipamento vinculada a camera operacional
        var res = await incService.RegisterAsync(new CreateIncidentDto
        {
            Type = IncidentType.EquipmentFailure,
            Severity = IncidentSeverity.Critical,
            EquipmentId = cam.Id,
            Description = "Perda total de video"
        });

        Assert.True(res.EquipmentStatusChanged);
        Assert.Equal(EquipmentStatus.Malfunctioning, (await eqRepo.GetByIdAsync(cam.Id))!.Status);
        Assert.NotNull(res.RiskAfter);
        Assert.True(res.RiskAfter!.Score >= 50); // Densidade (40) + Indisponibilidade (10) >= 50 (High)
        Assert.NotEmpty(res.NewRecommendations);

        // Aceitar recomendacao de manutencao ou deslocamento muda status para Accepted
        var rec = res.NewRecommendations.First();
        Assert.NotNull(rec.Explanation);
        var accepted = await recService.AcceptAsync(rec.Id);
        Assert.Equal(RecommendationStatus.Accepted, accepted.Status);
    }
}
