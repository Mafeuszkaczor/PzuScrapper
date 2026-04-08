using PzuScrapper;
using PzuScrapper.Configuration;

var session = AppSettingsLoader.LoadSiteSession();
var scrapper = new Scrapper(session);
await scrapper.Scrape();
