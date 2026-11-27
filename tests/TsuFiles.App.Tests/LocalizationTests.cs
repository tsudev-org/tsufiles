using System.Text.Json;
using System.Text.RegularExpressions;
using TsuFiles.App.Services;
using TsuFiles.Core.Formats;

namespace TsuFiles.App.Tests;

/// <summary>Keeps shipped languages in sync so none silently falls back to English.</summary>
public partial class LocalizationTests
{
    private static string I18nDirectory
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TsuFiles.slnx")))
                dir = dir.Parent;
            return Path.Combine(dir!.FullName, "src", "TsuFiles.App", "Assets", "i18n");
        }
    }

    private static Dictionary<string, string> Load(string code) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(I18nDirectory, code + ".json")))!;

    public static TheoryData<string> Languages => new(LocalizationService.Languages.Where(l => l.Code != "en").Select(l => l.Code));

    [Fact]
    public void Shared_language_registry_covers_every_shipped_translation_once()
    {
        var codes = LocalizationService.Languages.Select(l => l.Code).ToList();
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(Directory.GetFiles(I18nDirectory, "*.json").Select(Path.GetFileNameWithoutExtension).Order(), codes.Order());
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Catalogs_have_no_duplicate_keys_or_empty_translations(string code)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(I18nDirectory, code + ".json")));
        var entries = json.RootElement.EnumerateObject().ToList();
        Assert.Equal(entries.Count, entries.Select(e => e.Name).Distinct().Count());
        Assert.All(entries, e => Assert.False(string.IsNullOrWhiteSpace(e.Value.GetString()), $"{code}:{e.Name}"));
    }


    [Fact]
    public void The_flip_hint_leaves_its_arrow_to_the_donut()
    {
        // The hub draws the arrow itself (#554): as a character, fonts without "↻" (Linux's bundled Noto Sans) showed
        // a box. The key stays first, so every language still reads "Tab back".
        foreach (var code in LocalizationService.Languages.Select(l => l.Code))
        {
            var strings = Load(code);
            foreach (var key in new[] { "donut.flip_to_back", "donut.flip_to_front" })
            {
                Assert.DoesNotContain("↻", strings[key]);
                Assert.StartsWith("{0} ", strings[key]);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Every_language_has_exactly_the_english_keys(string code)
    {
        var english = Load("en").Keys.ToHashSet();
        var other = Load(code).Keys.ToHashSet();

        Assert.Empty(english.Except(other));
        Assert.Empty(other.Except(english));
    }

    [Fact]
    public void Toolbar_tabs_and_reused_categories_exist_in_every_language()
    {
        string[] required =
        [
            "toolbar.tab.all",
            "toolbar.tab.media",
            "toolbar.tab.edit",
            "toolbar.tab.model3d_cad",
            "format.category.Image",
            "format.category.Document",
            "format.category.Geo",
            "format.category.Archive"
        ];

        foreach (var language in LocalizationService.Languages)
        {
            var translations = Load(language.Code);
            foreach (var key in required)
            {
                Assert.True(translations.TryGetValue(key, out var value), $"{language.Code}:{key} is missing");
                Assert.False(string.IsNullOrWhiteSpace(value), $"{language.Code}:{key} is empty");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Line_breaks_are_real_line_breaks(string code)
    {
        // "\\n" in the JSON is a backslash and an n on screen; English writes "\n".
        foreach (var (key, value) in Load(code))
            Assert.False(value.Contains("\\n"), $"{code}:{key} has a literal \\n");
    }

    [Fact]
    public void Field_switch_texts_exist_in_every_language()
    {
        string[] required =
        [
            "nav.fields",
            "fields.title",
            "fields.description",
            "fields.formats",
            "fields.always_shown",
            "presets.target_hidden",
            .. FormatFields.All.Where(f => f.CanTurnOff).Select(f => $"fields.name.{f.Id}")
        ];
        foreach (var language in LocalizationService.Languages)
        {
            var translations = Load(language.Code);
            foreach (var key in required)
            {
                Assert.True(translations.TryGetValue(key, out var value), $"{language.Code}:{key} is missing");
                Assert.False(string.IsNullOrWhiteSpace(value), $"{language.Code}:{key} is empty");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Format_placeholders_match_english(string code)
    {
        var english = Load("en");
        foreach (var (key, value) in Load(code))
            Assert.True(
                Placeholders(english[key]).SetEquals(Placeholders(value)),
                $"{code}:{key} placeholders differ from English");
    }

    [Fact]
    public void Every_key_used_in_xaml_exists()
    {
        var english = Load("en");
        var src = Path.GetFullPath(Path.Combine(I18nDirectory, "..", ".."));
        var missing = Directory.EnumerateFiles(src, "*.axaml", SearchOption.AllDirectories)
            .SelectMany(f => DynamicResourceKey().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            .Where(k => k.Contains('.') && !k.StartsWith("Icon.", StringComparison.Ordinal))
            .Where(k => !english.ContainsKey(k))
            .Distinct()
            .ToList();

        Assert.Empty(missing);
    }

    [Theory]
    [InlineData("vi-VN", "vi")]
    [InlineData("vi", "vi")]
    [InlineData("VI-vn", "vi")]
    [InlineData("vin-TZ", "en")]
    [InlineData("en-US", "en")]
    // Languages earlier versions shipped now open in English (phiếu TG1).
    [InlineData("ko-KR", "en")]
    [InlineData("zh-CN", "en")]
    [InlineData("pt-BR", "en")]
    [InlineData("ar-EG", "en")]
    [InlineData("sv-SE", "en")]
    [InlineData("", "en")] // Linux without LANG: the invariant culture
    public void Auto_follows_the_language_the_user_reads_the_os_in(string os, string expected) =>
        Assert.Equal(expected, LocalizationService.Resolve("auto", () => os));

    [Theory]
    [InlineData("vi")]
    [InlineData("en")]
    public void A_chosen_language_is_kept_whatever_the_os_is_in(string chosen) =>
        // The OS isn't even asked: the user picked the language in Settings.
        Assert.Equal(chosen, LocalizationService.Resolve(chosen, () => throw new InvalidOperationException("asked the OS")));

    [Fact]
    public void Auto_is_not_the_language_filee_switched_to()
    {
        // Filee sets the current UI culture to the language it shows; "auto" must still mean the user's own.
        var before = LocalizationService.Resolve("auto");
        var shown = before == "vi" ? "en-US" : "vi-VN";
        var culture = System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo(shown);
            Assert.Equal(before, LocalizationService.Resolve("auto"));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = culture;
        }
    }

    [Fact]
    public void Vietnamese_is_offered_under_its_own_name()
    {
        Assert.Contains(("vi", "Tiếng Việt"), LocalizationService.Languages);
        Assert.Equal("vi", LocalizationService.Resolve("VI"));
    }

    [Fact]
    public void Only_vietnamese_and_english_ship()
    {
        Assert.Equal(["vi", "en"], LocalizationService.Languages.Select(l => l.Code));
        Assert.Equal("en", LocalizationService.Resolve("ko", () => "en-US"));
        Assert.Equal("vi", LocalizationService.Resolve("zh-CN", () => "vi-VN"));
    }

    /// <summary>Only Noto Sans is embedded; other scripts in file names take the system's fonts.</summary>
    [Fact]
    public void The_ui_font_is_noto_sans_then_the_system_fonts()
    {
        var families = ThemeService.FontFamilies("vi").Split(", ").ToList();
        Assert.Equal("fonts:Filee#Noto Sans", families[0]);
        Assert.Single(families, f => f.StartsWith("fonts:", StringComparison.Ordinal));
        Assert.Contains("Malgun Gothic", families);
        Assert.Contains("Microsoft YaHei", families);
        Assert.Equal("sans-serif", families[^1]);
        Assert.DoesNotContain("fonts:", SoftPalette.SystemFontFamilies("vi"));
    }

    private static HashSet<string> Placeholders(string text) =>
        PlaceholderPattern().Matches(text).Select(m => m.Value).ToHashSet();

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex PlaceholderPattern();

    [GeneratedRegex(@"\{DynamicResource ([\w.]+)\}")]
    private static partial Regex DynamicResourceKey();
}
