using Domain.Enums;
using Domain.Exceptions;
using Domain.Rules;
namespace Domain.Entities;

public abstract class Equipment : BaseEntity
{
    protected Equipment(EquipmentType type) => Type = type;

    public string SerialNumber { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;

    /// <summary>Discriminador TPH. Definido pela subclasse.</summary>
    public EquipmentType Type { get; protected set; }
    public EquipmentStatus Status { get; private set; } = EquipmentStatus.NotInstalled;
    public MaintenanceNeed MaintenanceNeed { get; set; } = MaintenanceNeed.None;
    public DateTime? NextMaintenanceDate { get; set; }

    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid AddressId { get; set; }
    public Address Address { get; set; } = null!;

    /// <summary>RD-02: aplica a matriz de transicao.</summary>
    public void ChangeStatus(EquipmentStatus next)
    {
        if (!EquipmentStatusRules.CanTransition(Status, next))
            throw new BusinessRuleException($"Transicao de status invalida: {Status} -> {next}.");
        Status = next;
    }
}
