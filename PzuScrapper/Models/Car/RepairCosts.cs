namespace PzuScrapper.Models.Car;

public class RepairCosts
    {
        public OriginalParts originalParts { get; set; }
        public AlternativeParts alternativeParts { get; set; }
        public ReplacementPartsSubtotal replacementPartsSubtotal { get; set; }
        public TinsmithLabor tinsmithLabor { get; set; }
        public PainterLabor painterLabor { get; set; }
        public PaintMaterials paintMaterials { get; set; }
        public AdditionalMaterials additionalMaterials { get; set; }
        public TotalCost totalCost { get; set; }
    }

