// The languages TsuFiles ships (Assets/i18n/<code>.json): Vietnamese and English only (phiếu TG1, 26/11/2026). The
// installer (src/TsuFiles.Setup) links this file, so a language added here is offered by Setup too.

namespace TsuFiles.App.Services;

internal static class AppLanguages
{
    /// <summary>Code → native name (shown in the language pickers of the app and of Setup).</summary>
    public static readonly IReadOnlyList<(string Code, string NativeName)> All =
    [
        ("vi", "Tiếng Việt"),
        ("en", "English"),
    ];

    /// <summary>
    /// The shipped language for a culture name: "vi" for Vietnamese ("vi", "vi-VN", not Vinza "vin"), "en" for anything
    /// else, including the languages earlier versions shipped (a saved "ko" or "zh-CN" opens in English).
    /// </summary>
    public static string Match(string? name)
    {
        var normalized = (name ?? "").Trim().Replace('_', '-');
        return normalized.Equals("vi", StringComparison.OrdinalIgnoreCase) || normalized.StartsWith("vi-", StringComparison.OrdinalIgnoreCase)
            ? "vi" : "en";
    }
}
