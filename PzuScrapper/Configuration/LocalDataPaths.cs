namespace PzuScrapper.Configuration;

internal static class LocalDataPaths
{
    public static string Cookies =>
        File.Exists("cookies.development.json") ? "cookies.development.json" : "cookies.json";

    public static string SessionStorage =>
        File.Exists("session-storage.development.json") ? "session-storage.development.json" : "session-storage.json";
}
