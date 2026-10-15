using System.Diagnostics;

namespace SplitProxy.Core.Programs;

public static class BrowserGuard
{
    private static readonly HashSet<string> BlockedExe = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome.exe", "msedge.exe", "firefox.exe", "browser.exe", "opera.exe", "opera_gx.exe",
        "brave.exe", "vivaldi.exe", "chromium.exe", "waterfox.exe", "thorium.exe", "zen.exe",
        "arc.exe", "iexplore.exe", "maxthon.exe", "palemoon.exe", "librewolf.exe", "duckduckgo.exe"
    };

    private static readonly HashSet<string> ProtectedExe = new(StringComparer.OrdinalIgnoreCase)
    {
        "splitproxy.exe", "splitproxy.service.exe", "splitproxy.routeengine.exe", "splitproxy.updater.exe",
        "svchost.exe", "services.exe", "lsass.exe", "winlogon.exe", "csrss.exe", "smss.exe", "system"
    };

    private static readonly string[] ProductMarkers =
    {
        "browser", "chrome", "chromium", "firefox", "microsoft edge", "yandex", "opera", "brave", "vivaldi",
        "waterfox", "librewolf", "pale moon", "maxthon", "duckduckgo"
    };

    public static bool IsBrowser(string path)
    {
        var exe = Path.GetFileName(path);
        if (BlockedExe.Contains(exe)) return true;
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            var text = $"{info.ProductName} {info.FileDescription} {info.CompanyName}".ToLowerInvariant();
            return ProductMarkers.Any(text.Contains);
        }
        catch { return false; }
    }

    public static bool IsForbidden(string path)
    {
        var exe = Path.GetFileName(path);
        return ProtectedExe.Contains(exe) || IsBrowser(path);
    }

    public static void ThrowIfForbidden(string path)
    {
        var exe = Path.GetFileName(path);
        if (ProtectedExe.Contains(exe))
            throw new InvalidOperationException("Эту системную программу нельзя добавлять в Split Proxy.");
        if (IsBrowser(path))
            throw new InvalidOperationException("Браузеры нельзя добавлять в Split Proxy.");
    }
}
