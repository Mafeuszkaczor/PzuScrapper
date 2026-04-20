namespace PzuScrapper.Models.Car;

public class Engine
    {
        public Type type { get; set; }
        public int power { get; set; }
        public PowerUnitMeasurement powerUnitMeasurement { get; set; }
        public int capacity { get; set; }
        public CapacityUnitMeasurement capacityUnitMeasurement { get; set; }
        public bool noEngineAndGearboxData { get; set; }
    }

