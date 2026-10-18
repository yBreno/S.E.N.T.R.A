using Domain.Entities;

namespace WebApplication1.backend.Domain.Entities
{
    public abstract class Equipment
    {
        public Guid Id { get; set; }
        public string SerialNumber { get; set; }
        public string Brand { get; set; }
        public string Model { get; set; }
        public Guid AddressId { get; set; }

        public EquipmentType Type { get; set; }
        public EquipmentStatus Status { get; set; }
        public MaintenanceNeed MaintenanceNeed { get; set; }
        public DateTime? NextMaintenanceDate { get; set; }
    }

    public class Camera : Equipment
    {
        public bool HasNightVision { get; set; }
        public string Resolution { get; set; } // Ex: "1080p", "4K"
        public bool IsColor { get; set; } // true para colorida, false para preto e branco
    }

    public class Radar : Equipment
    {
        public int? MaxSpeedLimit { get; set; } // Indicando a velocidade máxima, se aplicável
        public string RadarType { get; set; } // Ex: "Semáforo", "Velocidade", "Conversão proibida"
    }

    public class Sensor : Equipment
    {
        public string SensorType { get; set; } // Ex: "Temperatura", "Umidade", "Movimento"
        public string MeasurementUnit { get; set; } // Ex: "Celsius", "Fahrenheit", "Porcentagem"
    }

    public class Vehicle : Equipment
    {
        public string VehicleType { get; set; } // Ex: "Carro de patrulha", "Moto", "Caminhão"
        public string LicensePlate { get; set; }
        public string? Chassi { get; set; }
        public int? ManufactureYear { get; set; }
        public Guid? CurrentAgentId { get; set; } // Quem está usando o veículo no momento
    }

    public class  ElectricalScooter : Equipment
    {
        public string BatteryCapacity { get; set; } // Ex: "500Wh", "1000Wh"
        public int? MaxSpeed { get; set; } // Velocidade máxima em km/h
    }
}

