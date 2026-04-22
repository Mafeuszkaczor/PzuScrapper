namespace PzuScrapper;

/// <summary>Spójny, prosty wrapper na Console.WriteLine z kategorią (tagiem) w nawiasach kwadratowych.</summary>
internal static class Log
{
    public static void Info(string tag, string message) =>
        Console.WriteLine($"[{tag}] {message}");

    public static void Warn(string tag, string message) =>
        Console.WriteLine($"[{tag}] Ostrzeżenie: {message}");

    public static void Error(string tag, string message) =>
        Console.Error.WriteLine($"[{tag}] Błąd: {message}");

    /// <summary>Writes without newline (for progress lines, counters).</summary>
    public static void Raw(string text) => Console.Write(text);
}
