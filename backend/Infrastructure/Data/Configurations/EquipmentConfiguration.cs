using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Infrastructure.Data.Configurations;

/// <summary>Equipment em TPH: uma tabela "Equipments" com discriminador na coluna Type.</summary>
public class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> b)
    {
        b.ToTable("Equipments");
        b.HasKey(x => x.Id);
        b.Property(x => x.SerialNumber).IsRequired().HasMaxLength(80);
        b.Property(x => x.Brand).IsRequired().HasMaxLength(80);
        b.Property(x => x.Model).IsRequired().HasMaxLength(80);

        b.HasDiscriminator(x => x.Type)
            .HasValue<Camera>(EquipmentType.Camera)
            .HasValue<Radar>(EquipmentType.Radar)
            .HasValue<Sensor>(EquipmentType.Sensor)
            .HasValue<Vehicle>(EquipmentType.Vehicle)
            .HasValue<ElectricalScooter>(EquipmentType.ElectricalScooter)
            .HasValue<ParkingMeter>(EquipmentType.ParkingMeter)
            .HasValue<Motorcycle>(EquipmentType.Motorcycle)
            .HasValue<AutonomousVehicle>(EquipmentType.AutonomousVehicle);

        b.HasIndex(x => x.SerialNumber).IsUnique();
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.Type);

        b.HasOne(x => x.Address).WithMany().HasForeignKey(x => x.AddressId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class CameraConfiguration : IEntityTypeConfiguration<Camera>
{
    public void Configure(EntityTypeBuilder<Camera> b) => b.Property(x => x.Resolution).HasMaxLength(20);
}

public class RadarConfiguration : IEntityTypeConfiguration<Radar>
{
    public void Configure(EntityTypeBuilder<Radar> b) => b.Property(x => x.RadarType).HasMaxLength(60);
}

public class SensorConfiguration : IEntityTypeConfiguration<Sensor>
{
    public void Configure(EntityTypeBuilder<Sensor> b)
    {
        b.Property(x => x.SensorType).HasMaxLength(60);
        b.Property(x => x.MeasurementUnit).HasMaxLength(30);
    }
}

public class ElectricalScooterConfiguration : IEntityTypeConfiguration<ElectricalScooter>
{
    public void Configure(EntityTypeBuilder<ElectricalScooter> b) => b.Property(x => x.BatteryCapacity).HasMaxLength(30);
}

public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> b)
    {
        b.Property(x => x.LicensePlate).HasMaxLength(7);
        b.Property(x => x.Chassi).HasMaxLength(17);
        b.HasIndex(x => x.LicensePlate).IsUnique().HasFilter("[LicensePlate] IS NOT NULL");
        b.HasOne(x => x.CurrentAgent).WithMany().HasForeignKey(x => x.CurrentAgentId).OnDelete(DeleteBehavior.Restrict);
    }
}
