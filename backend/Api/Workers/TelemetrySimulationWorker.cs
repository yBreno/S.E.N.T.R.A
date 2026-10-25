using Application.DTOs.Predictive;
using Application.Interfaces.IServices;
using Domain.Enums;
using Domain.IRepositories;
using Microsoft.Extensions.Options;
namespace Api.Workers;

public class SimulationSettings
{
    public const string SectionName = "Simulation";
    public bool Enabled { get; set; } = false;
    public int IntervalSeconds { get; set; } = 30;
}


public class TelemetrySimulationWorker(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<SimulationSettings> options,
    ILogger<TelemetrySimulationWorker> logger) : BackgroundService
{
    private readonly Random _rng = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Aguarda inicializacao da API e seeds
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var cfg = options.CurrentValue;
            var delay = TimeSpan.FromSeconds(Math.Max(5, cfg.IntervalSeconds));

            if (cfg.Enabled)
            {
                try
                {
                    await SimulateCycleAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogDebug("Ciclo de simulacao de telemetria ignorado. Tipo={FailureType}", ex.GetType().Name);
                }
            }

            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task SimulateCycleAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var equipmentsRepo = scope.ServiceProvider.GetRequiredService<IEquipmentRepository>();
        var agentsRepo = scope.ServiceProvider.GetRequiredService<IAgentRepository>();
        var telemetryService = scope.ServiceProvider.GetRequiredService<ITelemetryService>();
        var agentLocationService = scope.ServiceProvider.GetRequiredService<IAgentLocationService>();

        var equipments = (await equipmentsRepo.SearchAsync(new EquipmentFilter(null, null, null), ct))
            .Where(e => e.Status is EquipmentStatus.Operational or EquipmentStatus.Malfunctioning)
            .ToList();

        if (equipments.Count > 0)
        {
            var eq = equipments[_rng.Next(equipments.Count)];
            var roll = _rng.NextDouble();
            var eventType = roll < 0.85 ? "Heartbeat" : (roll < 0.95 ? "Temperatura Elevada" : "Bateria Fraca");
            var payload = $"{{\"uptimeSec\":{_rng.Next(3600, 864000)},\"signalDbm\":-{_rng.Next(55, 85)}}}";

            await telemetryService.RegisterAsync(new CreateTelemetryDto
            {
                EquipmentId = eq.Id,
                EventType = eventType,
                PayloadData = payload,
                Latitude = eq.Address?.Latitude,
                Longitude = eq.Address?.Longitude
            }, ct);
        }

        var agents = (await agentsRepo.GetAllAsync(ct))
            .Where(a => a.Availability != AgentAvailability.Offline)
            .ToList();

        if (agents.Count > 0)
        {
            var ag = agents[_rng.Next(agents.Count)];
            var newLat = Math.Clamp(ag.Latitude + (_rng.NextDouble() - 0.5) * 0.001, -90, 90);
            var newLng = Math.Clamp(ag.Longitude + (_rng.NextDouble() - 0.5) * 0.001, -180, 180);

            await agentLocationService.UpdateLocationAsync(
                ag.Id,
                new UpdateAgentLocationDto
                {
                    Neighborhood = ag.Neighborhood,
                    Latitude = Math.Round(newLat, 6),
                    Longitude = Math.Round(newLng, 6),
                    Availability = ag.Availability
                },
                callerUserId: null,
                isStaff: true,
                ct);
        }
    }
}
