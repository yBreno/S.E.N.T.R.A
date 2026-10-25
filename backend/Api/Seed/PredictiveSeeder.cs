using Application.Interfaces.IServices;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace Api.Seed;


public static class PredictiveSeeder
{
    public static async Task SeedAsync(IServiceProvider services, ILogger logger)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        try
        {
            if (await db.Incidents.AnyAsync()) return;

            var now = DateTime.UtcNow;
            var rng = new Random(42); // deterministico para demonstracao e testes reproduziveis

            // 1. Enderecos por bairro
            var belaVista = new Address
            {
                Street = "Av. Paulista", Number = "1578", Neighborhood = "Bela Vista",
                City = "Sao Paulo", State = States.SP, ZipCode = "01310200",
                Latitude = -23.5614, Longitude = -46.6559
            };
            var centro = new Address
            {
                Street = "Praca da Se", Number = "100", Neighborhood = "Centro",
                City = "Sao Paulo", State = States.SP, ZipCode = "01001000",
                Latitude = -23.5505, Longitude = -46.6333
            };
            var pinheiros = new Address
            {
                Street = "Rua dos Pinheiros", Number = "500", Neighborhood = "Pinheiros",
                City = "Sao Paulo", State = States.SP, ZipCode = "05422000",
                Latitude = -23.5617, Longitude = -46.6820
            };
            var vilaMariana = new Address
            {
                Street = "Rua Domingos de Morais", Number = "1200", Neighborhood = "Vila Mariana",
                City = "Sao Paulo", State = States.SP, ZipCode = "04010100",
                Latitude = -23.5891, Longitude = -46.6347
            };
            var mooca = new Address
            {
                Street = "Rua da Mooca", Number = "2100", Neighborhood = "Mooca",
                City = "Sao Paulo", State = States.SP, ZipCode = "03104000",
                Latitude = -23.5558, Longitude = -46.5969
            };

            db.Addresses.AddRange(belaVista, centro, pinheiros, vilaMariana, mooca);

            // 2. Equipamentos (instalados ha 60 dias para permitir calculo real de MTBF)
            var installedAt = now.AddDays(-60);
            var camPaulista = new Camera
            {
                SerialNumber = "CAM-BV-001", Brand = "Axis", Model = "Q6135-LE",
                AddressId = belaVista.Id, HasNightVision = true, Resolution = "4K", IsColor = true, CreatedAt = installedAt
            };
            camPaulista.ChangeStatus(EquipmentStatus.Operational);

            var radPaulista = new Radar
            {
                SerialNumber = "RAD-BV-002", Brand = "Sensys", Model = "MSSS",
                AddressId = belaVista.Id, MaxSpeedLimit = 50, RadarType = "Fixo", CreatedAt = installedAt
            };
            radPaulista.ChangeStatus(EquipmentStatus.Operational);
            radPaulista.ChangeStatus(EquipmentStatus.Malfunctioning);
            radPaulista.MaintenanceNeed = MaintenanceNeed.Immediate;

            var camCentro = new Camera
            {
                SerialNumber = "CAM-CE-001", Brand = "Hikvision", Model = "DS-2DF8425IX",
                AddressId = centro.Id, HasNightVision = true, Resolution = "1080p", IsColor = true, CreatedAt = installedAt
            };
            camCentro.ChangeStatus(EquipmentStatus.Operational);

            var senCentro = new Sensor
            {
                SerialNumber = "SEN-CE-002", Brand = "Bosch", Model = "AirQ-200",
                AddressId = centro.Id, SensorType = "Alagamento/Pluviometro", MeasurementUnit = "mm/h", CreatedAt = installedAt
            };
            senCentro.ChangeStatus(EquipmentStatus.Operational);

            var radPinheiros = new Radar
            {
                SerialNumber = "RAD-PI-001", Brand = "Sensys", Model = "MSSS",
                AddressId = pinheiros.Id, MaxSpeedLimit = 60, RadarType = "Fixo", CreatedAt = installedAt
            };
            radPinheiros.ChangeStatus(EquipmentStatus.Operational);

            var camVilaMariana = new Camera
            {
                SerialNumber = "CAM-VM-001", Brand = "Intelbras", Model = "VIP-5450",
                AddressId = vilaMariana.Id, HasNightVision = true, Resolution = "1080p", IsColor = true, CreatedAt = installedAt
            };
            camVilaMariana.ChangeStatus(EquipmentStatus.Operational);

            var senMooca = new Sensor
            {
                SerialNumber = "SEN-MO-001", Brand = "Bosch", Model = "TrafficSense",
                AddressId = mooca.Id, SensorType = "Fluxo Veicular", MeasurementUnit = "veic/min", CreatedAt = installedAt
            };
            senMooca.ChangeStatus(EquipmentStatus.Operational);

            db.Equipments.AddRange(camPaulista, radPaulista, camCentro, senCentro, radPinheiros, camVilaMariana, senMooca);

            // 3. Agentes disponiveis para deslocamento preventivo (CPFs validos pelos digitos verificadores)
            var agents = new[]
            {
                new Agent
                {
                    Name = "Carlos Mendes", Cpf = "52998224725", BadgeNumber = "AG-101",
                    Role = AgentRole.TrafficAgent, Availability = AgentAvailability.Available,
                    Neighborhood = "Bela Vista", Latitude = -23.5625, Longitude = -46.6545, LocationUpdatedAt = now
                },
                new Agent
                {
                    Name = "Fernanda Rocha", Cpf = "11144477735", BadgeNumber = "AG-102",
                    Role = AgentRole.MunicipalGuard, Availability = AgentAvailability.Available,
                    Neighborhood = "Centro", Latitude = -23.5512, Longitude = -46.6340, LocationUpdatedAt = now
                },
                new Agent
                {
                    Name = "Roberto Lima", Cpf = "12345678909", BadgeNumber = "AG-103",
                    Role = AgentRole.PoliceOfficer, Availability = AgentAvailability.Available,
                    Neighborhood = "Pinheiros", Latitude = -23.5620, Longitude = -46.6810, LocationUpdatedAt = now
                }
            };
            db.Agents.AddRange(agents);

            // 4. Historico de falhas e manutencoes para MTBF e Falhas Recorrentes
            // 4a. radPaulista: 3 falhas nos ultimos 20 dias -> aciona Falha Recorrente no Risco + Anomalia de Falha Repetida
            db.Alerts.AddRange(
                new Alert
                {
                    EquipmentId = radPaulista.Id, Type = AlertType.Failure, Severity = AlertSeverity.High,
                    Message = "Conexao Perdida no radar Av. Paulista", Status = AlertStatus.Resolved,
                    CreatedAt = now.AddDays(-18), ResolvedAt = now.AddDays(-17)
                },
                new Alert
                {
                    EquipmentId = radPaulista.Id, Type = AlertType.Failure, Severity = AlertSeverity.High,
                    Message = "Erro de leitura Doppler no radar Av. Paulista", Status = AlertStatus.Resolved,
                    CreatedAt = now.AddDays(-9), ResolvedAt = now.AddDays(-8)
                },
                new Alert
                {
                    EquipmentId = radPaulista.Id, Type = AlertType.Failure, Severity = AlertSeverity.Critical,
                    Message = "Falha de hardware na fonte do radar Av. Paulista", Status = AlertStatus.Open,
                    CreatedAt = now.AddDays(-1)
                }
            );

            // 4b. camCentro: falhas a cada ~14 dias (dias -42, -28, -14) e ultima manutencao encerrada ha 13 dias
            // MTBF = 14 dias; decorridos = 13 dias (92.8% >= 85%) -> aciona Previsao de Indisponibilidade Futura (MTBF)!
            db.Alerts.AddRange(
                new Alert
                {
                    EquipmentId = camCentro.Id, Type = AlertType.Failure, Severity = AlertSeverity.Medium,
                    Message = "Queda de link optico Praca da Se", Status = AlertStatus.Resolved,
                    CreatedAt = now.AddDays(-42), ResolvedAt = now.AddDays(-41)
                },
                new Alert
                {
                    EquipmentId = camCentro.Id, Type = AlertType.Failure, Severity = AlertSeverity.Medium,
                    Message = "Reinicializacao inesperada de firmware", Status = AlertStatus.Resolved,
                    CreatedAt = now.AddDays(-28), ResolvedAt = now.AddDays(-27)
                },
                new Alert
                {
                    EquipmentId = camCentro.Id, Type = AlertType.Failure, Severity = AlertSeverity.High,
                    Message = "Bateria de backup degradada", Status = AlertStatus.Resolved,
                    CreatedAt = now.AddDays(-14), ResolvedAt = now.AddDays(-13)
                }
            );
            db.MaintenanceRecords.Add(new MaintenanceRecord
            {
                EquipmentId = camCentro.Id, AgentId = agents[2].Id,
                InitDate = now.AddDays(-14), EndDate = now.AddDays(-13),
                Description = "Troca de conector e revisao preventiva na Praca da Se",
                Cost = 450m, Urgency = MaintenanceUrgency.Medium, Status = MaintenanceStatus.Closed
            });

            // 5. Base historica de Incidentes (ultimas 6 semanas + pico anomalo recente em Bela Vista)
            var incidents = new List<Incident>();

            // Baseline historica nas ultimas 6 semanas no mesmo dia da semana/janela (1 a 2 ocorrencias por semana em Bela Vista)
            for (var week = 1; week <= 6; week++)
            {
                var baseTime = now.AddDays(-7 * week).AddMinutes(-30);
                incidents.Add(new Incident
                {
                    Type = IncidentType.TrafficAccident,
                    Severity = IncidentSeverity.Medium,
                    Neighborhood = "Bela Vista",
                    Latitude = belaVista.Latitude + (rng.NextDouble() - 0.5) * 0.006,
                    Longitude = belaVista.Longitude + (rng.NextDouble() - 0.5) * 0.006,
                    OccurredAt = baseTime,
                    CreatedAt = baseTime,
                    Description = $"Ocorrencia historica semana -{week} (Bela Vista)"
                });
                if (week % 2 == 0)
                {
                    incidents.Add(new Incident
                    {
                        Type = IncidentType.Congestion,
                        Severity = IncidentSeverity.Low,
                        Neighborhood = "Bela Vista",
                        Latitude = belaVista.Latitude + (rng.NextDouble() - 0.5) * 0.006,
                        Longitude = belaVista.Longitude + (rng.NextDouble() - 0.5) * 0.006,
                        OccurredAt = baseTime.AddMinutes(-25),
                        CreatedAt = baseTime.AddMinutes(-25),
                        Description = $"Congestionamento recorrente semana -{week} (Bela Vista)"
                    });
                }
            }

            // Pico atipico nas ultimas 2 horas em Bela Vista (6 ocorrencias -> Z-score > 2 sigma e risco elevado!)
            for (var i = 0; i < 6; i++)
            {
                var t = now.AddMinutes(-(15 * (i + 1)));
                incidents.Add(new Incident
                {
                    Type = i % 2 == 0 ? IncidentType.TrafficAccident : IncidentType.EquipmentFailure,
                    Severity = i < 2 ? IncidentSeverity.Critical : IncidentSeverity.High,
                    Neighborhood = "Bela Vista",
                    Latitude = belaVista.Latitude + (rng.NextDouble() - 0.5) * 0.004,
                    Longitude = belaVista.Longitude + (rng.NextDouble() - 0.5) * 0.004,
                    OccurredAt = t,
                    CreatedAt = t,
                    EquipmentId = i == 0 ? radPaulista.Id : null,
                    Description = $"Concentracao recente de chamados na Av. Paulista #{i + 1}"
                });
            }

            // Ocorrencias distribuidas nos ultimos 7 dias nos demais bairros para compor o Heatmap e o Indice de Risco
            var otherNeighborhoods = new (Address addr, int count, IncidentType mainType)[]
            {
                (centro, 5, IncidentType.PublicSafety),
                (pinheiros, 3, IncidentType.TrafficAccident),
                (vilaMariana, 2, IncidentType.Congestion),
                (mooca, 1, IncidentType.Flooding)
            };

            foreach (var (addr, count, mainType) in otherNeighborhoods)
            {
                for (var i = 0; i < count; i++)
                {
                    var t = now.AddHours(-(6 * (i + 1)));
                    incidents.Add(new Incident
                    {
                        Type = mainType,
                        Severity = i == 0 ? IncidentSeverity.High : IncidentSeverity.Medium,
                        Neighborhood = addr.Neighborhood,
                        Latitude = addr.Latitude + (rng.NextDouble() - 0.5) * 0.005,
                        Longitude = addr.Longitude + (rng.NextDouble() - 0.5) * 0.005,
                        OccurredAt = t,
                        CreatedAt = t,
                        Description = $"Ocorrencia registrada em {addr.Neighborhood} #{i + 1}"
                    });
                }
            }

            db.Incidents.AddRange(incidents);
            await db.SaveChangesAsync();

            // 6. Dispara a analise preditiva inicial para gerar alertas preventivos e recomendacoes explicaveis
            var analysis = scope.ServiceProvider.GetRequiredService<IPredictiveAnalysisService>();
            var summary = await analysis.RunAsync();
            logger.LogInformation(
                "Seed da Cidade Preditiva concluido: {Incidents} ocorrencias, {Recs} recomendacoes e {Alerts} alertas preventivos gerados.",
                incidents.Count, summary.RecommendationsCreated, summary.PreventiveAlertsCreated);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Seed da Cidade Preditiva ignorado (banco indisponivel ou migration pendente). Tipo={FailureType}", ex.GetType().Name);
        }
    }
}
