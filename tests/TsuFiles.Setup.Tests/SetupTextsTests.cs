// Setup speaks Filee's languages from the app's own translation files.

extern alias app;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TsuFiles.Engines.Infrastructure;
using TsuFiles.Setup.Platform;
using LocalizationService = app::TsuFiles.App.Services.LocalizationService;

namespace TsuFiles.Setup.Tests;

public partial class SetupTextsTests
{
    private static string Repository
    {
        get
        {
            var dir = AppContext.BaseDirectory;
            while (!File.Exists(Path.Combine(dir, "TsuFiles.slnx")))
                dir = Path.GetDirectoryName(dir) ?? throw new InvalidOperationException("Repository root not found.");
            return dir;
        }
    }

    [Fact]
    public void Setup_offers_every_language_filee_has()
    {
        Assert.Equal(LocalizationService.Languages.Select(l => (l.Code, l.NativeName)), SetupTexts.Languages);
        Assert.Equal([("vi", "Tiếng Việt"), ("en", "English")], SetupTexts.Languages);
        Assert.NotEqual("Install", new SetupTexts("vi")["installer.install"]);
    }

    [Theory]
    [InlineData(null, "vi-VN", "vi")]
    [InlineData("auto", "vi-VN", "vi")]
    [InlineData("auto", "en-US", "en")]
    [InlineData("vi", "en-US", "vi")]
    [InlineData("VI-vn", "en-US", "vi")]
    [InlineData("en", "vi-VN", "en")]
    // An older script's /LANG= with a language no longer shipped (phiếu TG1): English.
    [InlineData("ko", "vi-VN", "en")]
    [InlineData("zhcn", "en-US", "en")]
    [InlineData(null, "pt-BR", "en")]
    [InlineData(null, "vin-TZ", "en")]
    public void Languages_resolve_like_the_app_and_like_inno_setup(string? setting, string windows, string expected) =>
        Assert.Equal(expected, SetupTexts.Resolve(setting, () => windows));

    [Fact]
    public void Texts_fall_back_to_english_and_format()
    {
        var texts = new SetupTexts("vi");
        Assert.Equal("vi", texts.Language);
        Assert.NotEqual("installer.install", texts["installer.install"]);
        Assert.Contains("9.9.9", texts.Format("installer.welcome.fresh", "9.9.9"));
        Assert.Equal("no.such.key", texts["no.such.key"]);
        var changed = 0;
        texts.LanguageChanged += (_, _) => changed++;
        texts.SetLanguage("en");
        Assert.Equal(1, changed);
        Assert.Equal("Install", texts["installer.install"]);
    }

    [AvaloniaFact]
    public void Publishing_a_language_switch_raises_one_resource_change()
    {
        var app = Application.Current!;
        Assert.IsType<ResourceDictionary>(app.Resources); // Setup publishes to this dictionary in ApplyLook.
        var resources = new ResourceDictionary();
        resources["unrelated"] = "kept";
        var texts = new SetupTexts("en");
        texts.Publish(resources);
        app.Resources.MergedDictionaries.Add(resources);
        var changes = 0;
        void Count(object? sender, ResourcesChangedEventArgs e) => changes++;
        app.ResourcesChanged += Count;
        try
        {
            texts.SetLanguage("vi");
            texts.Publish(resources);

            Assert.Equal(1, changes);
            Assert.True(resources.TryGetResource("installer.install", null, out var install));
            Assert.Equal(texts["installer.install"], install);
            Assert.NotEqual("Install", install);
            Assert.True(resources.TryGetResource("general.language", null, out var language));
            Assert.Equal(texts["general.language"], language);
            Assert.Equal("kept", resources["unrelated"]);
        }
        finally
        {
            app.ResourcesChanged -= Count;
            app.Resources.MergedDictionaries.Remove(resources);
        }
    }

    [AvaloniaFact]
    public void A_language_switch_through_apply_look_shows_the_new_texts()
    {
        var app = Application.Current!;
        var texts = new SetupTexts("en");
        App.ApplyLook(app, texts);
        var block = new TextBlock();
        block.Bind(TextBlock.TextProperty, block.GetResourceObservable("installer.install"));
        var window = new Window { Content = block };
        window.Show();
        var changes = 0;
        void Count(object? sender, ResourcesChangedEventArgs e) => changes++;
        try
        {
            Assert.Equal("Install", block.Text);

            app.ResourcesChanged += Count;
            texts.SetLanguage("vi");

            var vietnamese = new SetupTexts("vi")["installer.install"];
            Assert.True(app.TryGetResource("installer.install", null, out var install));
            Assert.Equal(vietnamese, install);
            Assert.Equal(vietnamese, block.Text);
            // The texts are one change; the font and the flow direction add a few more. None of them grows with the
            // number of keys (one change per key was over a thousand).
            Assert.InRange(changes, 1, 10);
        }
        finally
        {
            window.Close();
            app.ResourcesChanged -= Count;
            texts.SetLanguage("en");
        }
    }

    [Fact]
    public void Every_key_setup_uses_exists_in_english()
    {
        var english = JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(Repository, "src", "TsuFiles.App", "Assets", "i18n", "en.json")))!;
        var source = Path.Combine(Repository, "src", "TsuFiles.Setup");
        var used = Directory.EnumerateFiles(source, "*.axaml", SearchOption.AllDirectories)
            .SelectMany(f => DynamicResource().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            .Concat(Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
                .SelectMany(f => KeyLiteral().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value)))
            .Where(k => k.Contains('.') && !k.EndsWith('.') && !k.StartsWith("Setup.", StringComparison.Ordinal))
            .Distinct()
            .ToList();
        Assert.Contains("installer.welcome.fresh", used);
        Assert.Contains("general.context_menu", used);
        Assert.DoesNotContain(used, k => !english.ContainsKey(k));
        // Engine names come from the app's own keys.
        Assert.All(EngineDownloads.Packages, p => Assert.True(english.ContainsKey($"engines.package.{p.Id}.name"), p.Id));
    }

    [Fact]
    public void Setup_looks_for_downloaded_engines_where_the_app_puts_them()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "The installer uses Windows settings and engine paths.");
        Assert.Equal(EngineEnvironment.DownloadRoot, SetupPaths.Real().EnginesFolder);
        Assert.Equal(TsuFiles.Core.Settings.UserDataStore.DefaultDirectory, SetupPaths.Real().SettingsFolder);
    }

    [GeneratedRegex(@"\{DynamicResource ([\w.]+)\}")]
    private static partial Regex DynamicResource();

    [GeneratedRegex(@"""((?:installer|setup|engines|general|common)\.[\w.]+)""")]
    private static partial Regex KeyLiteral();
}
