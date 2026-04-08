using System.Text.Json;
using PzuScrapper;
using PzuScrapper.Models;

var config = JsonDocument.Parse(File.ReadAllText("appsettings.json")).RootElement;

var session = new SiteSession
{
    Login    = config.GetProperty("PZU_LOGIN").GetString()    ?? throw new Exception("Brak PZU_LOGIN w appsettings.json"),
    Password = config.GetProperty("PZU_PASSWORD").GetString() ?? throw new Exception("Brak PZU_PASSWORD w appsettings.json"),
};

var scrapper = new Scrapper(session);
await scrapper.Scrape();
