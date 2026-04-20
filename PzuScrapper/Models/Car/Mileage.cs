namespace PzuScrapper.Models.Car;

public class Mileage
    {
        public int value { get; set; }
        public UnitMeasurement unitMeasurement { get; set; }
        public MeasurementType measurementType { get; set; }
        public bool noMileageData { get; set; }
    }

