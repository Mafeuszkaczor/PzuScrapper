using PzuScrapper.Configuration;
using PzuScrapper.Export;

namespace PzuScrapper;

/// <summary>
/// First screen of the console app: Enter = proceed to scraping,
/// "1" = settings submenu (clear cars.jsonl, etc.).
/// </summary>
internal static class StartupMenu
{
    /// <summary>Returns true when the user wants to continue to scraping.</summary>
    public static bool Run()
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("=== PzuScrapper ===");
            Console.WriteLine(" Enter — rozpocznij scraping");
            Console.WriteLine(" 1     — ustawienia");
            Console.WriteLine(" q     — zakończ");
            Console.Write("> ");
            Console.Out.Flush();

            var input = (Console.ReadLine() ?? string.Empty).Trim();

            switch (input.ToLowerInvariant())
            {
                case "":
                    return true;
                case "1":
                    RunSettingsMenu();
                    continue;
                case "q":
                case "quit":
                case "exit":
                    return false;
                default:
                    Log.Warn("Menu", $"Nieznana opcja: „{input}”.");
                    continue;
            }
        }
    }

    private static void RunSettingsMenu()
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("--- Ustawienia ---");
            Console.WriteLine(" 1 — wyczyść plik cars.jsonl (rozpocznij od zera)");
            Console.WriteLine(" 2 — zmień login i hasło PZU");
            Console.WriteLine(" 0 — wróć do menu głównego");
            Console.Write("> ");
            Console.Out.Flush();

            var input = (Console.ReadLine() ?? string.Empty).Trim();
            switch (input)
            {
                case "1":
                    ConfirmAndClearCarsFile();
                    break;
                case "2":
                    ChangeCredentials();
                    break;
                case "0":
                case "":
                    return;
                default:
                    Log.Warn("Ustawienia", $"Nieznana opcja: „{input}”.");
                    break;
            }
        }
    }

    private static void ConfirmAndClearCarsFile()
    {
        var path = AppPaths.CarsJsonLinesPath;
        Console.WriteLine();
        Console.WriteLine($"Na pewno wyczyścić plik {Path.GetFileName(path)}? (t/N)");
        Console.Write("> ");
        Console.Out.Flush();

        var answer = (Console.ReadLine() ?? string.Empty).Trim().ToLowerInvariant();
        if (answer is "t" or "tak" or "y" or "yes")
            AuctionIndex.Clear(path);
        else
            Log.Info("Ustawienia", "Anulowano.");
    }

    private static void ChangeCredentials()
    {
        if (CredentialsPrompt.PromptAndSave(warnIfOverwriting: true) is not null)
            Log.Info("Ustawienia", "Od teraz scraper używa nowego loginu.");
    }
}
