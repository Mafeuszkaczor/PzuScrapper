namespace PzuScrapper.Export;

/// <summary>Etykiety PL i wykluczenia pól (zgodnie z planem — pola oznaczone „x” w dokumencie planu).</summary>
internal static class CarDetailsPdfLabelMap
{
    /// <summary>Właściwości korzenia CarDetails pomijane w PDF.</summary>
    internal static readonly HashSet<string> ExcludedRootProperties = new(StringComparer.Ordinal)
    {
        nameof(PzuScrapper.Models.Car.CarDetails.id),
        nameof(PzuScrapper.Models.Car.CarDetails.auctionType),
        nameof(PzuScrapper.Models.Car.CarDetails.status),
        nameof(PzuScrapper.Models.Car.CarDetails.cancelReason),
        nameof(PzuScrapper.Models.Car.CarDetails.attachments),
        nameof(PzuScrapper.Models.Car.CarDetails.allowAutoCloseBeforeExpiration),
        nameof(PzuScrapper.Models.Car.CarDetails.externalId),
        nameof(PzuScrapper.Models.Car.CarDetails.relatedOffersCount),
        nameof(PzuScrapper.Models.Car.CarDetails.determiningBestOffer),
        nameof(PzuScrapper.Models.Car.CarDetails.vehicleCategory),
    };

    /// <summary>Pełne ścieżki właściwości (kropka) pomijane.</summary>
    internal static readonly HashSet<string> ExcludedPaths = new(StringComparer.Ordinal)
    {
        "auctionBasicData.biddingType",
        "auctionBasicData.externalSystem",
    };

    /// <summary>W dowolnym zagnieżdżeniu pomijane nazwy (np. słownikowe id/kod/aktywny).</summary>
    internal static readonly HashSet<string> ExcludedNestedPropertyNames = new(StringComparer.Ordinal)
    {
        "id",
        "code",
        "active",
    };

    internal static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
    {
        ["auctionUniqueNumber"] = "Numer oferty (unikalny)",
        ["createDate"] = "Data utworzenia oferty",
        ["auctionStartDate"] = "Data rozpoczęcia aukcji",
        ["productionYear"] = "Rok produkcji",
        ["sharedOwnership"] = "Współwłasność",
        ["leasing"] = "Leasing",
        ["bankLien"] = "Obciążenie bankowe / zastaw",
        ["cession"] = "Cesja",
        ["vatDeduction"] = "Odliczenie VAT",
        ["auctionBasicData"] = "Podstawowe dane aukcji",
        ["insuranceDamageDetails"] = "Dane szkody ubezpieczeniowej",
        ["manufacturer"] = "Marka pojazdu",
        ["model"] = "Model",
        ["type"] = "Wersja / typ pojazdu",
        ["comments"] = "Uwagi",
        ["additionalDamageRisk"] = "Dodatkowe ryzyko uszkodzenia",
        ["additionalDamageRiskDescription"] = "Opis dodatkowego ryzyka",
        ["bestOffer"] = "Najlepsza oferta",
        ["retailMargin"] = "Marża detaliczna",
        ["canRegisterComplaint"] = "Możliwość złożenia reklamacji",
        ["vin"] = "Numer VIN",
        ["registrationDate"] = "Data pierwszej rejestracji",
        ["technicalExaminationDate"] = "Data przeglądu technicznego",
        ["engine"] = "Silnik",
        ["mileage"] = "Przebieg",
        ["gearboxType"] = "Skrzynia biegów",
        ["vehiclePaintType"] = "Rodzaj lakieru",
        ["vehicleBodyType"] = "Typ nadwozia",
        ["doorsNumber"] = "Liczba drzwi",
        ["keysNumber"] = "Liczba kluczyków",
        ["privatelyImported"] = "Import indywidualny",
        ["firstOwner"] = "Pierwszy właściciel",
        ["vehicleCard"] = "Karta pojazdu",
        ["serviceBookAvailable"] = "Książka serwisowa",
        ["equipments"] = "Wyposażenie dodatkowe",
        ["missingDocuments"] = "Brakujące dokumenty",
        ["rollable"] = "Stan „na kołach” / możliwość toczenia",
        ["airbagDamaged"] = "Stan poduszek powietrznych",
        ["drivable"] = "Możliwość prowadzenia pojazdu",
        ["engineWorking"] = "Stan silnika (sprawność)",
        ["toCassation"] = "Przeznaczenie do kasacji",
        ["damageDescriptionRepair"] = "Opis uszkodzeń (naprawa)",
        ["damageDescriptionReplacedElements"] = "Opis wymienionych elementów",
        ["repairCosts"] = "Koszty naprawy",
        ["damageZones"] = "Strefy uszkodzeń",
        ["description"] = "Opis",
        ["location"] = "Lokalizacja",
        ["postalCode"] = "Kod pocztowy",
        ["country"] = "Kraj",
        ["vatRate"] = "Stawka VAT",
        ["currency"] = "Waluta",
        ["market"] = "Rynek",
        ["grossAmountBeforeDamage"] = "Kwota brutto przed uszkodzeniem",
        ["estimatedGrossResidueAmount"] = "Szacowana kwota brutto resztkowa",
        ["auctionItem"] = "Przedmiot aukcji",
        ["expirationDate"] = "Data wygaśnięcia oferty",
        ["ecCode"] = "Kod EC",
        ["insuranceDamageCreateDate"] = "Data zgłoszenia szkody",
        ["power"] = "Moc",
        ["powerUnitMeasurement"] = "Jednostka mocy",
        ["capacity"] = "Pojemność",
        ["capacityUnitMeasurement"] = "Jednostka pojemności",
        ["noEngineAndGearboxData"] = "Brak danych silnika/skrzyni",
        ["value"] = "Wartość przebiegu",
        ["unitMeasurement"] = "Jednostka",
        ["measurementType"] = "Typ pomiaru",
        ["noMileageData"] = "Brak danych o przebiegu",
        ["netCost"] = "Kwota netto",
        ["grossCost"] = "Kwota brutto",
        ["originalParts"] = "Części oryginalne",
        ["alternativeParts"] = "Części zamienne",
        ["replacementPartsSubtotal"] = "Suma części zamiennych",
        ["tinsmithLabor"] = "Robocizna blacharska",
        ["painterLabor"] = "Robocizna lakiernicza",
        ["paintMaterials"] = "Materiały lakiernicze",
        ["additionalMaterials"] = "Materiały dodatkowe",
        ["totalCost"] = "Suma całkowita",
    };

    internal static string GetLabel(string propertyName, string? fallbackPath = null) =>
        Labels.TryGetValue(propertyName, out var l)
            ? l
            : (fallbackPath != null && Labels.TryGetValue(fallbackPath, out var l2) ? l2 : propertyName);
}
