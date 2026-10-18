namespace Application.DTOs.Dashboard;
public record DashboardDto(int TotalEquipments, Dictionary<string, int> ByStatus, double AvailabilityRatePercent, int ActiveAgents);
