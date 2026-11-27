// "What's new" slides: the manifests and their media, when the slides are due (update, fresh install, downgrade,
// background start), the window's buttons and keys, and screenshots of the window.

using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TsuFiles.App.Controls;
using TsuFiles.App.Services;
using TsuFiles.App.ViewModels;
using TsuFiles.App.ViewModels.Pages;
using TsuFiles.App.Views;
using TsuFiles.App.Views.Pages;
using TsuFiles.Core.Localization;
using TsuFiles.Core.Settings;

namespace TsuFiles.App.Tests;

// Motion: one test switches reduced motion, which the window reads when it opens.
[Collection("Motion")]
public class WhatsNewTests
{
    private static readonly Version V2 = new(2, 0, 0);

    private static SlideDeck Deck(string version) =>
        new(SlideDeck.TryParseVersion(version)!, [new Slide("a", new Uri("avares://TsuFiles/a.webp"), "t", "x")]);

    // ───────────────────────── Manifests ─────────────────────────

    [AvaloniaTheory]
    [InlineData("2.0.0")]
    [InlineData("2.3.0")]
    public void A_deck_ships_with_its_media_and_texts(string version)
    {
        TestServices.EnsureInitialized("en");
        var decks = SlideDeck.LoadAll("TsuFiles");
        var deck = Assert.Single(decks, d => d.Version == new Version(version));
        Assert.InRange(deck.Slides.Count, 5, 7);
        Assert.Equal(deck.Slides.Count, deck.Slides.Select(s => s.Id).Distinct().Count());
        var english = LoadStrings("en");
        foreach (var slide in deck.Slides)
        {
            Assert.True(AssetLoader.Exists(slide.Media), $"{slide.Media} is missing");
            Assert.StartsWith($"avares://TsuFiles/Assets/WhatsNew/{version}/", slide.Media.ToString(), StringComparison.Ordinal);
            Assert.Contains(slide.TitleKey, english.Keys);
            Assert.Contains(slide.TextKey, english.Keys);
        }
    }

    /// <summary>The media are product assets that ship in every installer: each small, all of them small together.</summary>
    [Fact]
    public void Slide_media_stay_small()
    {
        var root = Path.Combine(WhatsNewMedia.RepositoryRoot, "src", "TsuFiles.App", "Assets", "WhatsNew");
        var media = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).Select(f => new FileInfo(f)).ToList();
        Assert.NotEmpty(media);
        Assert.All(media, f => Assert.True(f.Length <= 400 * 1024, $"{f.Name} is {f.Length / 1024} KB"));
        foreach (var pack in media.GroupBy(f => f.DirectoryName))
            Assert.True(pack.Sum(f => f.Length) <= 2 * 1024 * 1024, $"{pack.Key} adds up to more than 2 MB");
        Assert.True(media.Sum(f => f.Length) <= 6 * 1024 * 1024, "the shared and Portuguese media exceed 6 MB");
    }

    [Theory]
    [InlineData("pt-PT", "pt-PT/crop.webp", 4)]
    [InlineData("PT-br", "pt-BR/crop.webp", 7)]
    [InlineData("pt", "crop.webp", 2)]
    [InlineData("fr", "crop.webp", 2)]
    [InlineData(null, "crop.webp", 2)]
    public void Localized_media_use_an_exact_variant_and_their_own_poster(string? language, string path, int poster)
    {
        var folder = new Uri("avares://TsuFiles/Assets/WhatsNew/2.0.0/");
        var deck = SlideDeck.Parse("""
            { "version": "2.0.0", "slides": [
              { "id": "crop", "media": "crop.webp", "poster": 2, "title": "t", "text": "x",
                "localizedMedia": {
                  "pt-PT": { "media": "pt-PT/crop.webp", "poster": 4 },
                  "pt-BR": { "media": "pt-BR/crop.webp", "poster": 7 },
                  "fr": { "media": "../../secret.webp" },
                  "es": { "media": "https://example.com/crop.webp" },
                  "de": "bad entry"
                } }
            ] }
            """, folder)!;
        var slide = Assert.Single(deck.Slides);
        Assert.Equal(2, slide.LocalizedMedia.Count);
        Assert.Equal(new SlideMedia(new Uri(folder, path), poster), slide.MediaFor(language));
    }

    [AvaloniaTheory]
    [InlineData("pt-PT")]
    [InlineData("pt-BR")]
    public void Portuguese_recordings_preserve_the_desktop_context_and_localize_the_other_slides(string language)
    {
        TestServices.EnsureInitialized(language);
        var deck = Assert.Single(SlideDeck.LoadAll("TsuFiles"), d => d.Version == V2);
        foreach (var slide in deck.Slides)
        {
            var media = slide.MediaFor(language);
            Assert.True(AssetLoader.Exists(media.Source), $"{media.Source} is missing");
            if (slide.Id is "languages" or "browser" or "flip")
                Assert.Equal(slide.Media, media.Source);
            else
                Assert.Contains($"/{language}/", media.Source.AbsolutePath);
        }
    }

    [AvaloniaFact]
    public void Changing_language_refreshes_the_current_recording_without_resetting_the_slide()
    {
        var window = Open("vi");
        var loc = AppHost.Get<LocalizationService>();
        try
        {
            var slides = window.SlideShow;
            slides.GoTo(3);
            foreach (var language in new[] { "vi", "en", "vi" })
            {
                loc.SetLanguage(language);
                Pump();
                Assert.Equal(3, slides.Index);
                Assert.Equal(slides.Slides[3].MediaFor(language).Source, slides.Media.Source);
                Assert.True(slides.Media.HasPicture);
                Assert.Equal(slides.Slides[3].MediaFor(language).PosterFrame, slides.Media.PosterFrame);
            }
        }
        finally
        {
            window.Close();
            TestServices.EnsureInitialized("en");
        }
    }

    [Fact]
    public void A_manifest_is_read_leniently_and_safely()
    {
        var folder = new Uri("avares://TsuFiles/Assets/WhatsNew/3.1.0/");
        var deck = SlideDeck.Parse("""
            {
              // comments and trailing commas are fine
              "version": "v3.1",
              "slides": [
                { "id": "one", "media": "one.webp", "poster": 12, "title": "a.title", "text": "a.text" },
                { "id": "no-text", "media": "two.webp", "title": "b.title" },
                { "id": "escape", "media": "../../secret.png", "title": "c.title", "text": "c.text" },
                { "id": "rooted", "media": "C:/x.png", "title": "d.title", "text": "d.text" },
                { "id": "slash", "media": "/x.png", "title": "e.title", "text": "e.text" },
                { "id": "web", "media": "https://example.com/x.png", "title": "f.title", "text": "f.text" },
              ],
            }
            """, folder);

        Assert.NotNull(deck);
        Assert.Equal(new Version(3, 1, 0), deck.Version);
        Assert.Equal("3.1", deck.DisplayVersion);
        var slide = Assert.Single(deck.Slides);
        Assert.Equal(new Uri("avares://TsuFiles/Assets/WhatsNew/3.1.0/one.webp"), slide.Media);
        Assert.Equal(("a.title", "a.text"), (slide.TitleKey, slide.TextKey));
        Assert.Equal(12, slide.PosterFrame);

        Assert.Null(SlideDeck.Parse("""{ "version": "soon", "slides": [ { "id": "a", "media": "a.webp", "title": "t", "text": "x" } ] }""", folder));
        Assert.Null(SlideDeck.Parse("""{ "version": "3.0.0", "slides": [] }""", folder));
        Assert.Null(SlideDeck.Parse("[]", folder));
        Assert.Equal("2.1.3", Deck("2.1.3").DisplayVersion);
    }

    [Fact]
    public void Versions_compare_by_three_parts()
    {
        Assert.Equal(V2, SlideDeck.TryParseVersion("2.0"));
        Assert.Equal(V2, SlideDeck.TryParseVersion("v2.0.0-beta+abc"));
        Assert.Equal(V2, SlideDeck.TryParseVersion("2"));
        Assert.Null(SlideDeck.TryParseVersion(""));
        Assert.Null(SlideDeck.TryParseVersion("next"));
    }

    // ───────────────────────── The installer's switch ─────────────────────────

    [Fact]
    public void The_installer_switch_after_an_update_is_still_accepted()
    {
        var options = CommandLine.Parse(["--background", "--whats-new"]);
        Assert.True(options.Background);
        Assert.True(options.WhatsNew);
        Assert.False(CommandLine.Parse(["--background"]).WhatsNew);
        Assert.Null(options.UpdatedFrom);
        Assert.Equal("1.12.0", CommandLine.Parse(["--whats-new=1.12.0"]).UpdatedFrom);
    }

    // ───────────────────────── The window ─────────────────────────

    private static WhatsNewWindow Open(string language = "en", Action? beforeOpening = null, Version? version = null)
    {
        TestServices.EnsureInitialized(language);
        beforeOpening?.Invoke();
        var deck = SlideDeck.LoadAll("TsuFiles").Single(d => d.Version == (version ?? V2));
        var window = new WhatsNewWindow(deck, AppHost.Get<ILocalizer>());
        window.Show();
        window.Activate();
        Pump();
        return window;
    }

    private static Button Named(Window window, string name) =>
        window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == name);

    private static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Pump();
    }

    private static void Press(Window window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
        if (window.IsVisible) // Esc closed it
            window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
        Pump();
    }

    [AvaloniaFact]
    public void Next_and_previous_walk_the_slides_and_the_last_one_closes()
    {
        var window = Open();
        var slides = window.SlideShow;
        var (previous, primary) = (Named(window, "PreviousButton"), Named(window, "PrimaryButton"));
        var loc = AppHost.Get<ILocalizer>();
        try
        {
            Assert.Equal(0, slides.Index);
            Assert.False(previous.IsEnabled);
            Assert.Equal(0, previous.Opacity);
            Assert.Equal(loc["whats_new.next"], primary.Content);
            Assert.Equal(loc.Format("whats_new.title", "2.0"), window.Title);

            Click(primary);
            Assert.Equal(1, slides.Index);
            Assert.True(previous.IsEnabled);
            Click(previous);
            Assert.Equal(0, slides.Index);

            for (var i = 1; i < slides.Slides.Count; i++)
                Click(primary);
            Assert.True(slides.IsLast);
            Assert.Equal(loc["whats_new.done"], primary.Content);
            Assert.True(window.IsVisible);
            Click(primary); // "Get started"
            Assert.False(window.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData("en")]
    [InlineData("pt-PT")]
    [InlineData("pt-BR")]
    public void Arrow_keys_move_and_escape_closes(string language)
    {
        var window = Open(language);
        try
        {
            Press(window, Key.Right);
            Press(window, Key.Right);
            Assert.Equal(2, window.SlideShow.Index);
            Press(window, Key.Left);
            Assert.Equal(1, window.SlideShow.Index);
            Press(window, Key.Left);
            Press(window, Key.Left); // stays on the first
            Assert.Equal(0, window.SlideShow.Index);
            Press(window, Key.Escape);
            Assert.False(window.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void In_arabic_left_goes_on_and_next_sits_on_the_left()
    {
        var window = Open("ar");
        try
        {
            Assert.Equal(Avalonia.Media.FlowDirection.RightToLeft, window.FlowDirection);
            Press(window, Key.Left);
            Press(window, Key.Left);
            Assert.Equal(2, window.SlideShow.Index);
            Press(window, Key.Right);
            Assert.Equal(1, window.SlideShow.Index);
            var previous = Named(window, "PreviousButton");
            var next = Named(window, "PrimaryButton");
            Assert.True(next.TranslatePoint(default, window)!.Value.X < previous.TranslatePoint(default, window)!.Value.X);
        }
        finally
        {
            window.Close();
            TestServices.EnsureInitialized("en");
        }
    }

    [AvaloniaFact]
    public void The_close_button_closes_from_any_slide()
    {
        var window = Open();
        try
        {
            window.SlideShow.GoTo(3);
            Click(Named(window, "CloseButton"));
            Assert.False(window.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData("en")]
    [InlineData("vi")]
    public void Reduced_motion_shows_each_slides_poster_frame(string language)
    {
        var before = Motion.Enabled;
        var window = Open(language, beforeOpening: () => Motion.Set(false)); // after the theme, which applies the setting
        try
        {
            var media = window.SlideShow.Media;
            Assert.False(window.SlideShow.PlayAnimations);
            Assert.True(media.HasPicture);
            Assert.True(media.FrameCount > 1);
            // Each recording has its own meaningful poster: the localized ones open with the donut already visible.
            var poster = window.SlideShow.Slides[0].MediaFor(language).PosterFrame;
            if (language == "en")
                Assert.True(poster > 0);
            Assert.Equal(poster, media.CurrentFrame);
            Assert.False(media.IsPlaying);
            for (var i = 0; i < window.SlideShow.Slides.Count; i++)
            {
                window.SlideShow.GoTo(i);
                Pump();
                var expected = window.SlideShow.Slides[i].MediaFor(language).PosterFrame;
                Assert.InRange(expected, 0, media.FrameCount - 1);
                Assert.Equal(expected, media.CurrentFrame);
                Assert.True(media.HasPicture);
                Assert.False(media.IsPlaying);
            }
            window.SlideShow.GoTo(0);
            Pump();

            Motion.Set(true);
            Pump();
            Assert.True(window.SlideShow.PlayAnimations);
            Assert.True(media.IsPlaying);


            // Switched off while open: back to the poster.
            media.Tick();
            Motion.Set(false);
            Pump();
            Assert.Equal(poster, media.CurrentFrame);
            Assert.False(media.IsPlaying);
        }
        finally
        {
            window.Close();
            Motion.Set(before);
        }
    }

    [AvaloniaFact]
    public void The_slides_keep_one_height_and_let_go_of_their_pictures_when_closed()
    {
        var window = Open("es");
        var heights = new HashSet<double>();
        for (var i = 0; i < window.SlideShow.Slides.Count; i++)
        {
            window.SlideShow.GoTo(i);
            Pump();
            heights.Add(window.Bounds.Height);
            Assert.True(window.SlideShow.Media.HasPicture, $"slide {i} has no picture");
        }
        Assert.Single(heights);
        var media = window.SlideShow.Media;
        window.Close();
        Pump();
        Assert.False(media.HasPicture);
    }

    [AvaloniaFact]
    public void About_offers_the_slides_again()
    {
        TestServices.EnsureInitialized("en");
        var service = AppHost.Get<WhatsNewService>();
        var store = AppHost.Get<UserDataStore>();
        var version = service.CurrentVersion;
        try
        {
            service.CurrentVersion = new Version(2, 0, 3);
            var vm = new MainWindowViewModel(AppHost.Services, AppHost.Get<ILocalizer>(), AppHost.Get<UpdateService>(), AppHost.Get<UserDataStore>());
            vm.Navigate("about");
            var window = new MainWindow { DataContext = vm, Width = 1080, Height = 820 };
            window.Show();
            Pump();
            var about = (AboutPageViewModel)vm.CurrentPage!;
            Assert.Equal(AppHost.Get<ILocalizer>().Format("whats_new.about_link", "2.0"), about.WhatsNewText);
            var link = Named(window, "WhatsNewLink");
            Assert.True(link.IsEffectivelyVisible);
            Save(window, "page-about-whats-new-en.png");

            link.Command!.Execute(null);
            Pump();
            Assert.IsType<WhatsNewWindow>(AppHost.Get<WindowService>().WhatsNew);
            AppHost.Get<WindowService>().WhatsNew!.Close();
            window.Close();

            // A version older than every deck has nothing to offer.
            service.CurrentVersion = new Version(1, 12, 0);
            using var older = new AboutPageViewModel(AppHost.Get<ILocalizer>(), store, AppHost.Get<UpdateService>(), service);
            Assert.False(older.HasWhatsNew);
        }
        finally
        {
            service.CurrentVersion = version;
        }
    }

    // ───────────────────────── Screenshots ─────────────────────────

    [AvaloniaTheory]
    [InlineData("en", false)]
    [InlineData("en", true)]
    [InlineData("ar", false)]
    [InlineData("ar", true)]
    [InlineData("es", false)]
    [InlineData("es", true)]
    [InlineData("hi", false)]
    [InlineData("ko", true)]
    [InlineData("ja", false)]
    [InlineData("zh-CN", false)]
    [InlineData("pl", false)]
    [InlineData("ru", false)]
    [InlineData("ru", true)]
    [InlineData("pt-BR", false)]
    [InlineData("pt-BR", true)]
    [InlineData("pt-PT", false)]
    [InlineData("pt-PT", true)]
    [InlineData("fr", false)]
    public void Window_renders(string language, bool dark)
    {
        TestServices.EnsureInitialized(language);
        var theme = AppHost.Get<ThemeService>();
        var window = Open(language, () => theme.Apply(new ThemeSettings { Mode = dark ? ThemeMode.Dark : ThemeMode.Light }, language));
        var suffix = $"{language}-{(dark ? "dark" : "light")}";
        try
        {
            Save(window, $"whatsnew-window-{suffix}-1.png");
            window.SlideShow.GoTo(3);
            Pump();
            Save(window, $"whatsnew-window-{suffix}-4.png");
            window.SlideShow.GoTo(window.SlideShow.Slides.Count - 1);
            Pump();
            Save(window, $"whatsnew-window-{suffix}-last.png");
        }
        finally
        {
            window.Close();
            theme.Apply(new ThemeSettings(), "en");
        }
    }

    /// <summary>
    /// The 2.3.0 slides in a few languages: every slide's text fits and, with reduced motion, shows its poster frame
    /// (what the installer shows too). A screenshot of each slide.
    /// </summary>
    [AvaloniaTheory]
    [InlineData("en", false)]
    [InlineData("en", true)]
    [InlineData("ko", false)]
    [InlineData("ar", false)]
    [InlineData("de", false)]
    [InlineData("ja", false)]
    [InlineData("it", false)]
    [InlineData("id", false)]
    public void The_2_3_0_slides_render_on_their_posters(string language, bool dark)
    {
        TestServices.EnsureInitialized(language);
        var theme = AppHost.Get<ThemeService>();
        var motion = Motion.Enabled;
        var window = Open(language, () =>
        {
            theme.Apply(new ThemeSettings { Mode = dark ? ThemeMode.Dark : ThemeMode.Light }, language);
            Motion.Set(false);
        }, new Version(2, 3, 0));
        try
        {
            var slides = window.SlideShow;
            Assert.Equal(5, slides.Slides.Count);
            var heights = new HashSet<double>();
            for (var i = 0; i < slides.Slides.Count; i++)
            {
                slides.GoTo(i);
                Pump();
                heights.Add(window.Bounds.Height);
                var media = slides.Media;
                Assert.True(media.HasPicture, $"slide {i} has no picture");
                Assert.InRange(slides.Slides[i].PosterFrame, 0, media.FrameCount - 1);
                Assert.Equal(slides.Slides[i].PosterFrame, media.CurrentFrame);
                Save(window, $"whatsnew-2.3.0-{language}-{(dark ? "dark" : "light")}-{i + 1}-{slides.Slides[i].Id}.png");
            }
            Assert.Single(heights);
        }
        finally
        {
            window.Close();
            Motion.Set(motion);
            theme.Apply(new ThemeSettings(), "en");
        }
    }

    private static Dictionary<string, string> LoadStrings(string code) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(
            Path.Combine(WhatsNewMedia.RepositoryRoot, "src", "TsuFiles.App", "Assets", "i18n", code + ".json")))!;

    private static void Pump()
    {
        for (var i = 0; i < 20; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private static void Save(TopLevel window, string name)
    {
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        using var stream = File.Create(Path.Combine(TestServices.ScreenshotDirectory, name));
        frame.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }
}
