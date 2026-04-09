using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using PzuScrapper.Models;

namespace PzuScrapper.Configuration;

/// <summary>Loads host configuration and credentials from appsettings / environment.</summary>
internal static class AppConfiguration
{
    public static void AddOptionalDevelopmentJson(HostApplicationBuilder builder)
    {
        if (File.Exists("appsettings.development.json"))
            builder.Configuration.AddJsonFile("appsettings.development.json", optional: true, reloadOnChange: true);
    }

    public static SiteSession CreateSiteSession(IConfiguration configuration)
    {
        var login = configuration["PZU_LOGIN"];
        var password = configuration["PZU_PASSWORD"];

        if (string.IsNullOrWhiteSpace(login))
            throw new InvalidOperationException(
                "PZU_LOGIN is missing — set it in appsettings.json, appsettings.Development.json, or environment variables.");
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "PZU_PASSWORD is missing — set it the same way as PZU_LOGIN.");

        return new SiteSession { Login = login, Password = password };
    }
}
