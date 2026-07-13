using System.Text;
using PzuScrapper.Models;

namespace PzuScrapper.Configuration;

/// <summary>
/// Interactive console prompt for PZU login + password.
/// Stores the result via <see cref="CredentialsStore"/>.
/// </summary>
internal static class CredentialsPrompt
{
    /// <summary>Prompts for login/password and persists them. Returns the session on success, null if cancelled.</summary>
    public static SiteSession? PromptAndSave(bool warnIfOverwriting)
    {
        Console.WriteLine();
        Console.WriteLine("--- Login i hasło PZU ---");
        if (warnIfOverwriting && CredentialsStore.Exists)
            Console.WriteLine("(Masz już zapisane dane logowania — ta operacja je nadpisze.)");
        Console.WriteLine("Enter bez wpisywania czegokolwiek = anuluj.");

        Console.WriteLine();
        Console.Write("Login: ");
        Console.Out.Flush();
        var login = (Console.ReadLine() ?? string.Empty).Trim();
        if (login.Length == 0)
        {
            Log.Info("Ustawienia", "Anulowano.");
            return null;
        }

        Console.Write("Hasło: ");
        Console.Out.Flush();
        var password = ReadMaskedLine();
        if (password.Length == 0)
        {
            Log.Info("Ustawienia", "Anulowano.");
            return null;
        }

        try
        {
            CredentialsStore.Save(login, password);
            Log.Info("Ustawienia", $"Zapisano dane logowania (użytkownik: {login}).");
            return new SiteSession { Login = login, Password = password };
        }
        catch (Exception ex)
        {
            Log.Error("Ustawienia", $"Nie udało się zapisać danych logowania: {ex.Message}");
            return null;
        }
    }

    private static string ReadMaskedLine()
    {
        var buffer = new StringBuilder();
        while (true)
        {
            ConsoleKeyInfo key;
            try
            {
                key = Console.ReadKey(intercept: true);
            }
            catch (InvalidOperationException)
            {
                return (Console.ReadLine() ?? string.Empty).Trim();
            }

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return buffer.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Length > 0)
                {
                    buffer.Length--;
                    Console.Write("\b \b");
                }
                continue;
            }

            if (key.Key == ConsoleKey.Escape)
            {
                Console.WriteLine();
                return string.Empty;
            }

            if (char.IsControl(key.KeyChar))
                continue;

            buffer.Append(key.KeyChar);
            Console.Write('*');
        }
    }
}
