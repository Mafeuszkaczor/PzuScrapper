namespace PzuScrapper.Configuration;

internal static class ImportPaths
{
    /// <summary>Desktop/import/dd.MM.yyyy — eksport PDF dla nowych ofert.</summary>
    public static string TodayImportDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "import",
            DateTime.Today.ToString("dd.MM.yyyy"));

    /// <summary>Bezpieczna nazwa pliku z numeru aukcji.</summary>
    public static string SanitizeFileName(string auctionUniqueNumber)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var s = string.Join("_", auctionUniqueNumber.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(s) ? "auction" : s.Trim();
    }

    public static string PdfPathForAuction(string auctionUniqueNumber) =>
        Path.Combine(TodayImportDirectory, $"{SanitizeFileName(auctionUniqueNumber)}.pdf");
}
