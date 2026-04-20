using System.Collections.Generic;

namespace Models;

/// <summary>Vehicle category codes to human-readable labels (API filters / console prompts).</summary>
public static class VehicleCategoryCodes
{
    public static IReadOnlyDictionary<string, string> Catalog { get; } = new Dictionary<string, string>
    {
        ["BUS"] = "Bus",
        ["SCI"] = "samochód ciężarowy",
        ["SDO"] = "samochód dostawczy",
        ["INN"] = "inne pojazdy",
        ["MAR"] = "maszyny rolnicze",
        ["MOT"] = "motocykle",
        ["OGL"] = "ogólne",
        ["PIN"] = "przyczepy i naczepy",
        ["SAO"] = "samochód osobowy",
        ["SAT"] = "samochód terenowy",
    };
}
