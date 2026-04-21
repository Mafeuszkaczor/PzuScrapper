using Microsoft.Extensions.Hosting;
using PzuScrapper.Configuration;
using PzuScrapper.Search;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var builder = Host.CreateApplicationBuilder(args);
Console.WriteLine("[Konfiguracja] Start programu.");

AppConfiguration.AddOptionalDevelopmentJson(builder);

var session = AppConfiguration.CreateSiteSession(builder.Configuration);
var searchFilters = BidderSearchFiltersPrompt.Read();

await new PzuScrapper.ScrapeOrchestrator(session, builder.Environment, searchFilters).RunAsync();
