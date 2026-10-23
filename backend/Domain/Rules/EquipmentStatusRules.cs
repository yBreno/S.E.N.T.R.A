using Domain.Enums;
namespace Domain.Rules;

/// <summary>RD-02: matriz de transicao de EquipmentStatus.</summary>
public static class EquipmentStatusRules
{
    private static readonly Dictionary<EquipmentStatus, EquipmentStatus[]> Allowed = new()
    {
        [EquipmentStatus.NotInstalled] = [EquipmentStatus.Operational, EquipmentStatus.Deactivated, EquipmentStatus.Stolen],
        [EquipmentStatus.Operational] = [EquipmentStatus.Malfunctioning, EquipmentStatus.UnderMaintenance, EquipmentStatus.Deactivated, EquipmentStatus.Stolen],
        [EquipmentStatus.Malfunctioning] = [EquipmentStatus.UnderMaintenance, EquipmentStatus.Deactivated, EquipmentStatus.Stolen],
        [EquipmentStatus.UnderMaintenance] = [EquipmentStatus.Operational, EquipmentStatus.Deactivated],
        [EquipmentStatus.Deactivated] = [EquipmentStatus.Operational],
        [EquipmentStatus.Stolen] = [],
    };

    public static bool CanTransition(EquipmentStatus from, EquipmentStatus to) => Allowed[from].Contains(to);

    /// <summary>Transicoes que exigem perfil de gestor (Admin/OperationManager).</summary>
    public static bool RequiresManager(EquipmentStatus from, EquipmentStatus to) =>
        from == EquipmentStatus.Deactivated && to == EquipmentStatus.Operational;
}
