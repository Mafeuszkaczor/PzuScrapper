namespace PzuScrapper.Configuration;

internal static class AutaCsvPaths
{
    private static string CommonApplicationDataPath =>
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

    private static string AppDataDirectory => System.IO.Path.Combine(CommonApplicationDataPath, "PzuScrapper");

    public static string CarsJsonLinesPath => System.IO.Path.Combine(AppDataDirectory, "cars.jsonl");

    /// <summary>Photo output folder: CommonApplicationData/PzuScrapper/photos/{auction number or VIN}/</summary>
    public static string PhotosDirectory => System.IO.Path.Combine(AppDataDirectory, "photos");
}
