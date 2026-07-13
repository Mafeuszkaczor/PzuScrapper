using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Playwright;

namespace PzuScrapper.Auth;

/// <summary>
/// Zrzuca pełny stan strony + wszystkich ramek (screenshot, HTML, lista pól)
/// do <c>~/Desktop/pzu-2fa-dump/&lt;timestamp&gt;-&lt;reason&gt;/</c>.
/// Uruchamiane przy wykrywaniu 2FA, szczególnie w przypadku timeoutów.
/// </summary>
internal static class AuthDiagnostics
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Czy włączony jest tryb diagnostyczny (ENV <c>PZU_DEBUG=1</c>).</summary>
    public static bool IsEnabled =>
        string.Equals(Environment.GetEnvironmentVariable("PZU_DEBUG"), "1", StringComparison.Ordinal)
        || string.Equals(Environment.GetEnvironmentVariable("PZU_DEBUG"), "true", StringComparison.OrdinalIgnoreCase);

    public static async Task DumpAsync(IPage page, string reason)
    {
        try
        {
            var dir = CreateDumpDirectory(reason);

            await SavePageScreenshotAsync(page, dir);
            await SavePageHtmlAsync(page, dir);
            await SaveFramesAsync(page, dir);
            await SaveSummaryAsync(page, dir, reason);

            Log.Info("Diag", $"Zrzut 2FA: {dir}");
        }
        catch (Exception ex)
        {
            Log.Warn("Diag", $"Nie udało się zapisać diagnostyki ({reason}): {ex.Message}");
        }
    }

    private static string CreateDumpDirectory(string reason)
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Desktop",
            "pzu-2fa-dump");
        var safeReason = SanitizeForFileName(reason);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var dir = Path.Combine(root, $"{stamp}-{safeReason}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static async Task SavePageScreenshotAsync(IPage page, string dir)
    {
        try
        {
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(dir, "page.png"),
                FullPage = true,
            });
        }
        catch (PlaywrightException ex)
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "page.png.error.txt"), ex.ToString());
        }
    }

    private static async Task SavePageHtmlAsync(IPage page, string dir)
    {
        try
        {
            var html = await page.ContentAsync();
            await File.WriteAllTextAsync(Path.Combine(dir, "page.html"), html);
            await File.WriteAllTextAsync(Path.Combine(dir, "page.url.txt"), page.Url);
        }
        catch (PlaywrightException ex)
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "page.html.error.txt"), ex.ToString());
        }
    }

    private static async Task SaveFramesAsync(IPage page, string dir)
    {
        var framesDir = Path.Combine(dir, "frames");
        Directory.CreateDirectory(framesDir);

        var index = 0;
        foreach (var frame in page.Frames)
        {
            var label = SanitizeForFileName($"{index:00}-{(string.IsNullOrWhiteSpace(frame.Name) ? "unnamed" : frame.Name)}");
            var framePrefix = Path.Combine(framesDir, label);

            try
            {
                await File.WriteAllTextAsync($"{framePrefix}.url.txt", frame.Url);

                var html = await frame.ContentAsync();
                await File.WriteAllTextAsync($"{framePrefix}.html", html);

                var elements = await CollectFrameElementsAsync(frame);
                await File.WriteAllTextAsync(
                    $"{framePrefix}.elements.json",
                    JsonSerializer.Serialize(elements, JsonOpts));
            }
            catch (PlaywrightException ex)
            {
                await File.WriteAllTextAsync($"{framePrefix}.error.txt", ex.ToString());
            }

            index++;
        }
    }

    private static async Task<object> CollectFrameElementsAsync(IFrame frame)
    {
        const string script = """
            () => {
                const describe = (el) => {
                    const rect = el.getBoundingClientRect();
                    const style = window.getComputedStyle(el);
                    return {
                        tag: el.tagName.toLowerCase(),
                        id: el.id || null,
                        name: el.getAttribute('name'),
                        type: el.getAttribute('type'),
                        placeholder: el.getAttribute('placeholder'),
                        maxlength: el.getAttribute('maxlength'),
                        inputmode: el.getAttribute('inputmode'),
                        autocomplete: el.getAttribute('autocomplete'),
                        role: el.getAttribute('role'),
                        ariaLabel: el.getAttribute('aria-label'),
                        className: el.className || null,
                        text: (el.innerText || el.textContent || '').trim().slice(0, 200),
                        value: 'value' in el ? (el.value ?? null) : null,
                        visible: !!(rect.width && rect.height && style.visibility !== 'hidden' && style.display !== 'none'),
                        rect: { x: rect.x, y: rect.y, w: rect.width, h: rect.height },
                    };
                };
                const q = (sel) => Array.from(document.querySelectorAll(sel)).map(describe);
                return {
                    url: location.href,
                    title: document.title,
                    inputs: q('input'),
                    textareas: q('textarea'),
                    buttons: q('button'),
                    iframes: Array.from(document.querySelectorAll('iframe')).map(el => ({
                        id: el.id || null,
                        name: el.getAttribute('name'),
                        src: el.getAttribute('src'),
                        visible: !!(el.getBoundingClientRect().width && el.getBoundingClientRect().height),
                    })),
                };
            }
            """;

        return await frame.EvaluateAsync<object>(script);
    }

    private static async Task SaveSummaryAsync(IPage page, string dir, string reason)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"reason: {reason}");
        sb.AppendLine($"timestamp: {DateTime.Now:O}");
        sb.AppendLine($"url: {page.Url}");
        sb.AppendLine($"frames: {page.Frames.Count}");
        sb.AppendLine();
        sb.AppendLine("frames listing:");
        var i = 0;
        foreach (var f in page.Frames)
        {
            sb.AppendLine($"  [{i:00}] name='{f.Name}' url='{f.Url}' parent='{(f.ParentFrame is null ? "<main>" : f.ParentFrame.Name)}'");
            i++;
        }

        await File.WriteAllTextAsync(Path.Combine(dir, "SUMMARY.txt"), sb.ToString());
    }

    private static string SanitizeForFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(c => invalid.Contains(c) || c == ' ' || c == '/' ? '_' : c);
        var cleaned = new string(chars.ToArray()).Trim('_');
        return string.IsNullOrWhiteSpace(cleaned) ? "unknown" : cleaned;
    }
}
