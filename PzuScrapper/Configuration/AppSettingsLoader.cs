using System.Text.Json.Nodes;
using PzuScrapper.Models;

namespace PzuScrapper.Configuration;

internal static class AppSettingsLoader
{
    private const string BaseFile = "appsettings.json";
    private const string DevelopmentFile = "appsettings.development.json";

    public static SiteSession LoadSiteSession()
    {
        if (!File.Exists(BaseFile))
            throw new FileNotFoundException($"Brak pliku {BaseFile}.");

        var root = JsonNode.Parse(File.ReadAllText(BaseFile))!.AsObject();

        if (File.Exists(DevelopmentFile))
        {
            var dev = JsonNode.Parse(File.ReadAllText(DevelopmentFile))!.AsObject();
            foreach (var kv in dev)
                root[kv.Key] = kv.Value?.DeepClone();
        }

        var login = root["PZU_LOGIN"]?.GetValue<string>();
        var password = root["PZU_PASSWORD"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(login))
            throw new InvalidOperationException("Brak PZU_LOGIN (uzupełnij appsettings.json lub appsettings.development.json).");
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Brak PZU_PASSWORD (uzupełnij appsettings.json lub appsettings.development.json).");

        return new SiteSession { Login = login, Password = password };
    }
}
