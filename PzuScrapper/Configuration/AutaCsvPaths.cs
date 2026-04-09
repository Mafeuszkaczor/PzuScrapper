namespace PzuScrapper.Configuration;

internal static class AutaCsvPaths
{
    private static string Desktop => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    public static string DesktopAutaCsv => Path.Combine(Desktop, "auta.csv");

    /// <summary>Photo output folder: Desktop\photos\{auction number or VIN}\</summary>
    public static string DesktopPhotosDirectory => Path.Combine(Desktop, "photos");
}
