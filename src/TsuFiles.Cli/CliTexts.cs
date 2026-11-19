// TsuFiles: văn bản của dòng lệnh, đọc từ file dịch của ứng dụng (nhúng sẵn): thông báo lỗi của từng file và tên
// các preset có sẵn. Tiếng Việt (vi.json) khi hệ thống dùng tiếng Việt, ngoài ra tiếng Anh; thiếu chuỗi nào thì lấy tiếng
// Anh (en.json). Biến môi trường TSUFILES_LANG=vi|en thắng ngôn ngữ hệ thống (xem CliLanguage).

using System.Globalization;
using System.Text.Json;
using TsuFiles.Core;
using TsuFiles.Core.Localization;

namespace TsuFiles.Cli;

/// <summary>Ngôn ngữ của dòng lệnh: "vi" hoặc "en", mặc định theo ngôn ngữ hệ thống (PRODUCT_POLICY mục 1).</summary>
internal static class CliLanguage
{
    /// <summary>Biến môi trường chọn ngôn ngữ dòng lệnh.</summary>
    public const string EnvironmentVariable = "TSUFILES_LANG";

    /// <summary>Ngôn ngữ đang dùng. Chỉ nhận danh sách trắng "vi" | "en"; giá trị lạ theo ngôn ngữ hệ thống.</summary>
    public static string Current { get; set; } =
        FromEnvironment(Environment.GetEnvironmentVariable(EnvironmentVariable), CultureInfo.CurrentUICulture.Name);

    public static bool IsVietnamese => Current == "vi";

    internal static string FromEnvironment(string? value, string? systemLanguage) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "en" or "en-us" or "english" => "en",
            "vi" or "vi-vn" or "vietnamese" => "vi",
            _ => ForkDefaults.CommandLineLanguage(systemLanguage),
        };

    /// <summary>Chọn câu theo ngôn ngữ đang dùng.</summary>
    public static string T(string vietnamese, string english) => ForkDefaults.Brand(IsVietnamese ? vietnamese : english);
}

internal sealed class CliTexts : ILocalizer
{
    private readonly Dictionary<string, string> _texts;
    private readonly Dictionary<string, string> _fallback;

    public CliTexts() : this(CliLanguage.Current) { }

    public CliTexts(string language)
    {
        Language = language == "vi" ? "vi" : "en";
        _fallback = Load("en");
        _texts = Language == "en" ? _fallback : Load(Language);
    }

    private static Dictionary<string, string> Load(string code)
    {
        using var stream = typeof(CliTexts).Assembly.GetManifestResourceStream($"TsuFiles.Cli.{code}.json")
                           ?? throw new InvalidOperationException($"Embedded {code}.json is missing.");
        using var json = JsonDocument.Parse(stream);
        return json.RootElement.EnumerateObject()
            .Where(p => p.Value.ValueKind == JsonValueKind.String)
            .ToDictionary(p => p.Name, p => ForkDefaults.Brand(p.Value.GetString()!));
    }

    public string Language { get; }

    public string this[string key] =>
        _texts.TryGetValue(key, out var text) || _fallback.TryGetValue(key, out text) ? text : key;

    public string Format(string key, params object[] args) => string.Format(CultureInfo.InvariantCulture, this[key], args);

    public event EventHandler? LanguageChanged { add { } remove { } }
}
