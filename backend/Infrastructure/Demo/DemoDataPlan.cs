using System.Security.Cryptography;
using System.Text;
using Application.DTOs.Address;
using Application.DTOs.Agent;
using Application.DTOs.Equipment;
using Application.DTOs.Predictive;
using Application.Validators;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;

namespace Infrastructure.Demo;

/// <summary>Fixed identities, relative dates, and exclusively synthetic operational data. No users or credentials.</summary>
public sealed record DemoDataPlan(
    IReadOnlyList<Address> Addresses, IReadOnlyList<Equipment> Equipments,
    IReadOnlyList<Agent> Agents, IReadOnlyList<Incident> Incidents,
    IReadOnlyList<Alert> Alerts, IReadOnlyList<MaintenanceRecord> Maintenances)
{
    public const string Prefix = "SENTRA-DEMO-v1";
    public string Dataset { get; init; } = Prefix;
    public DateTime? ReferenceUtc { get; init; }
    public IReadOnlyList<TelemetryLog> Telemetry { get; init; } = [];
    public IReadOnlyList<Recommendation> Recommendations { get; init; } = [];

    public static DemoDataPlan Create(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
            throw new ArgumentException("A referencia deve estar em UTC.", nameof(utcNow));

        (string Name, double Lat, double Lon)[] neighborhoods =
        [
            ("Centro", -23.5505, -46.6333), ("Bela Vista", -23.5614, -46.6559),
            ("Pinheiros", -23.5617, -46.6820), ("Vila Mariana", -23.5891, -46.6347),
            ("Mooca", -23.5558, -46.5969)
        ];
        var addresses = new List<Address>();
        var equipments = new List<Equipment>();
        var agents = new List<Agent>();
        var incidents = new List<Incident>();
        var alerts = new List<Alert>();
        var maintenances = new List<MaintenanceRecord>();

        for (var i = 0; i < 60; i++)
        {
            var n = neighborhoods[i % 5];
            // Small deterministic offsets; coordinates are illustrations, not actual device locations.
            var address = new Address
            {
                Id = Id("address", i), CreatedAt = utcNow.AddDays(-60),
                Street = $"Via ficticia DEMO {i + 1:000}", Number = "S/N",
                Neighborhood = n.Name, City = "Sao Paulo", State = States.SP, ZipCode = "00000000",
                LocationReference = $"{Prefix}/address/{i:000}",
                Latitude = n.Lat + ((i / 5) % 4 - 1.5) * 0.001,
                Longitude = n.Lon + ((i / 20) - 1) * 0.001
            };
            addresses.Add(address);
            Equipment equipment = (i % 8) switch
            {
                0 => new Camera { HasNightVision = true, Resolution = "1080p", IsColor = true },
                1 => new Radar { MaxSpeedLimit = 50, RadarType = "DEMO Fixo" },
                2 => new Sensor { SensorType = "DEMO Nivel", MeasurementUnit = "cm" },
                3 => new Vehicle(),
                4 => new ElectricalScooter { BatteryCapacity = "DEMO 400 Wh", MaxSpeed = 20 },
                5 => new ParkingMeter { Spots = 4 },
                6 => new Motorcycle { EngineCapacityCc = 150 },
                _ => new AutonomousVehicle { AutonomyLevel = 3 }
            };
            equipment.Id = Id("equipment", i);
            equipment.CreatedAt = address.CreatedAt;
            equipment.SerialNumber = $"{Prefix}-EQ-{i:000}";
            equipment.Brand = "DEMO";
            equipment.Model = $"DEMO {equipment.Type}";
            equipment.AddressId = address.Id;
            if (equipment is Vehicle vehicle)
            {
                vehicle.LicensePlate = $"DMO{i:0000}";
                vehicle.ManufactureYear = 2024;
            }
            var status = (EquipmentStatus)(i % 6);
            if (status is EquipmentStatus.Operational or EquipmentStatus.Malfunctioning or EquipmentStatus.UnderMaintenance)
                equipment.ChangeStatus(EquipmentStatus.Operational);
            if (status is EquipmentStatus.Malfunctioning or EquipmentStatus.UnderMaintenance)
            {
                equipment.ChangeStatus(EquipmentStatus.Malfunctioning);
                equipment.MaintenanceNeed = MaintenanceNeed.Immediate;
            }
            if (status == EquipmentStatus.UnderMaintenance)
            {
                equipment.ChangeStatus(status);
                maintenances.Add(new MaintenanceRecord
                {
                    Id = Id("maintenance", i), CreatedAt = utcNow.AddHours(-12),
                    EquipmentId = equipment.Id, InitDate = utcNow.AddHours(-12),
                    Description = $"{Prefix}/maintenance/{i:000} - manutencao sintetica",
                    Urgency = MaintenanceUrgency.High, Status = MaintenanceStatus.Open
                });
            }
            if (status is EquipmentStatus.Deactivated or EquipmentStatus.Stolen) equipment.ChangeStatus(status);
            equipments.Add(equipment);
        }

        for (var i = 0; i < 20; i++)
        {
            var n = neighborhoods[i % 5];
            agents.Add(new Agent
            {
                Id = Id("agent", i), CreatedAt = utcNow.AddDays(-30),
                Name = $"DEMO Agente {i + 1:000}", BadgeNumber = $"{Prefix}-AG-{i:000}",
                // Algorithmic test document, never obtained from a person. Not proof of an unassigned CPF.
                Cpf = SyntheticCpf(i), Role = (AgentRole)(i % 3),
                Availability = (AgentAvailability)(i % 3), UserId = null,
                Neighborhood = n.Name, Latitude = n.Lat + (i / 5 + 1) * 0.0004,
                Longitude = n.Lon - (i / 5 + 1) * 0.0004, LocationUpdatedAt = utcNow.AddMinutes(-i)
            });
        }

        IncidentType[] otherTypes = [IncidentType.TrafficAccident, IncidentType.Congestion,
            IncidentType.Vandalism, IncidentType.Flooding, IncidentType.PublicSafety, IncidentType.Other];
        for (var i = 0; i < 100; i++)
        {
            var n = neighborhoods[i % 5];
            var failure = i < 25;
            var equipment = equipments.First(e => addresses.Single(a => a.Id == e.AddressId).Neighborhood == n.Name
                && e.Status == EquipmentStatus.Malfunctioning);
            var address = addresses.Single(a => a.Id == equipment.AddressId);
            var occurredAt = utcNow.AddMinutes(-(i + 1) * 75);
            incidents.Add(new Incident
            {
                Id = Id("incident", i), CreatedAt = occurredAt, OccurredAt = occurredAt,
                Type = failure ? IncidentType.EquipmentFailure : otherTypes[(i - 25) % otherTypes.Length],
                Severity = (IncidentSeverity)(i % 4), Neighborhood = n.Name,
                Latitude = failure ? address.Latitude : n.Lat + (i % 7 - 3) * 0.0005,
                Longitude = failure ? address.Longitude : n.Lon + (i % 9 - 4) * 0.0005,
                EquipmentId = failure ? equipment.Id : null,
                Description = $"{Prefix}/incident/{i:000} - ocorrencia sintetica"
            });
        }
        for (var i = 0; i < 15; i++)
        {
            var n = neighborhoods[i % 5];
            var equipment = equipments.First(e => addresses.Single(a => a.Id == e.AddressId).Neighborhood == n.Name
                && e.Status == EquipmentStatus.Malfunctioning);
            alerts.Add(new Alert
            {
                Id = Id("alert", i), CreatedAt = utcNow.AddHours(-(i + 1)),
                EquipmentId = equipment.Id, Type = AlertType.Failure,
                Severity = (AlertSeverity)(i % 4), Status = AlertStatus.Open,
                Message = $"{Prefix}/alert/{i:000} - falha sintetica DEMO"
            });
        }
        var plan = new DemoDataPlan(addresses, equipments, agents, incidents, alerts, maintenances);
        plan.Validate();
        return plan;
    }

    private static Guid Id(string category, int index) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"{Prefix}/{category}/{index:000}"))[..16]);

    private static string SyntheticCpf(int index)
    {
        var digits = (900000001 + index).ToString(System.Globalization.CultureInfo.InvariantCulture);
        for (var size = 9; size <= 10; size++)
        {
            var sum = digits.Select((digit, i) => (digit - '0') * (size + 1 - i)).Sum();
            var check = sum * 10 % 11;
            digits += check == 10 ? "0" : check.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        return digits;
    }

    public void Validate()
    {
        var addressValidator = new CreateAddressDtoValidator();
        var agentValidator = new CreateAgentDtoValidator();
        var equipmentValidator = new CreateEquipmentDtoValidator();
        var incidentValidator = new CreateIncidentDtoValidator();
        foreach (var a in Addresses) addressValidator.ValidateAndThrow(new CreateAddressDto
        {
            Street = a.Street, Neighborhood = a.Neighborhood, City = a.City, State = a.State,
            ZipCode = a.ZipCode, Country = a.Country, Latitude = a.Latitude, Longitude = a.Longitude
        });
        foreach (var a in Agents)
        {
            var result = agentValidator.Validate(new CreateAgentDto
            {
                Name = a.Name, Cpf = a.Cpf, BadgeNumber = a.BadgeNumber, Role = a.Role,
                Availability = a.Availability, Neighborhood = a.Neighborhood, Latitude = a.Latitude, Longitude = a.Longitude
            });
            // Exceção exclusiva da fixture: não enfraquece o validador do cadastro real.
            var errors = result.Errors.Where(e => !(Dataset == DemoEvaluationPlan.Prefix &&
                DemoEvaluationPlan.IsSyntheticDocument(a.Cpf) && e.PropertyName == nameof(CreateAgentDto.Cpf))).ToList();
            if (errors.Count > 0) throw new ValidationException(errors);
        }
        foreach (var e in Equipments) equipmentValidator.ValidateAndThrow(new CreateEquipmentDto
        {
            SerialNumber = e.SerialNumber, Brand = e.Brand, Model = e.Model, AddressId = e.AddressId,
            Type = e.Type, LicensePlate = (e as Vehicle)?.LicensePlate,
            ManufactureYear = (e as Vehicle)?.ManufactureYear,
            MaxSpeedLimit = (e as Radar)?.MaxSpeedLimit, MaxSpeed = (e as ElectricalScooter)?.MaxSpeed,
            Spots = (e as ParkingMeter)?.Spots
        });
        foreach (var i in Incidents) incidentValidator.ValidateAndThrow(new CreateIncidentDto
        {
            Type = i.Type, Severity = i.Severity, Description = i.Description, Neighborhood = i.Neighborhood,
            Latitude = i.Latitude, Longitude = i.Longitude, EquipmentId = i.EquipmentId, OccurredAt = i.OccurredAt
        });
        if (Equipments.Any(e => !Addresses.Any(a => a.Id == e.AddressId)) ||
            Incidents.Any(i => i.EquipmentId.HasValue && !Equipments.Any(e => e.Id == i.EquipmentId)) ||
            Alerts.Any(a => !Equipments.Any(e => e.Id == a.EquipmentId)) ||
            Maintenances.Any(m => !Equipments.Any(e => e.Id == m.EquipmentId) || (m.AgentId.HasValue && !Agents.Any(a => a.Id == m.AgentId))) ||
            Telemetry.Any(t => !Equipments.Any(e => e.Id == t.EquipmentId)) ||
            Recommendations.Any(r => r.EquipmentId.HasValue && !Equipments.Any(e => e.Id == r.EquipmentId)) || Agents.Any(a => a.UserId.HasValue))
            throw new InvalidOperationException("Relacionamento DEMO invalido.");
    }
}
