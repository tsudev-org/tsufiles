using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Platform;
using Avalonia.VisualTree;
using TsuFiles.App.Controls;
using TsuFiles.App.Services;
using TsuFiles.App.ViewModels;
using TsuFiles.App.Views;
using TsuFiles.Core.Localization;
using TsuFiles.Core.Settings;

namespace TsuFiles.App.Tests;

public class LocalizationResourceTests
{
    [AvaloniaFact]
    public void Strings_are_available_as_application_resources()
    {
        TestServices.EnsureInitialized("vi");
        Assert.True(Application.Current!.TryGetResource("home.title", null, out var value));
        Assert.Equal("Trang chủ", value);
    }

    [AvaloniaFact]
    public void DynamicResource_in_xaml_resolves_and_follows_language_changes()
    {
        TestServices.EnsureInitialized("en");
        var text = new TextBlock();
        text.Bind(TextBlock.TextProperty, text.GetResourceObservable("home.title"));
        var toggle = new ToggleSwitch();
        var window = new Window { Content = new StackPanel { Children = { text, toggle } } };
        window.Show();
        Assert.Equal("Home", text.Text);

        TestServices.EnsureInitialized("vi");
        Assert.Equal("Trang chủ", text.Text);
        Assert.Equal("Bật", toggle.OnContent);
        Assert.Equal("Tắt", toggle.OffContent);
        TestServices.EnsureInitialized("en");
        Assert.Equal("Home", text.Text);
        window.Close();
    }

    /// <summary>
    /// A language switch publishes its strings in one resource change: a change per key reached every control of every
    /// window still alive, and made the later App tests minutes slower (#540).
    /// </summary>
    [AvaloniaFact]
    public void A_language_switch_publishes_its_strings_in_one_resource_change()
    {
        TestServices.EnsureInitialized("en");
        var app = Application.Current!;
        var changes = 0;
        void Count(object? sender, ResourcesChangedEventArgs e) => changes++;
        app.ResourcesChanged += Count;
        try
        {
            AppHost.Get<LocalizationService>().SetLanguage("vi");
            Assert.Equal(1, changes);
            Assert.True(app.TryGetResource("home.title", null, out var title));
            Assert.Equal("Trang chủ", title);
        }
        finally
        {
            app.ResourcesChanged -= Count;
            TestServices.EnsureInitialized("en");
        }
    }

    /// <summary>A switch before the dictionary is attached still takes effect, and attaching later shows the last language.</summary>
    [AvaloniaFact]
    public void A_language_set_before_attaching_shows_once_attached()
    {
        TestServices.EnsureInitialized("en");
        var app = Application.Current!;
        var merged = app.Resources.MergedDictionaries;
        var count = merged.Count;
        var localization = new LocalizationService();
        var changed = 0;
        localization.LanguageChanged += (_, _) => changed++;
        try
        {
            localization.SetLanguage("vi");
            Assert.Equal("vi", localization.Language);
            Assert.Equal("vi", System.Globalization.CultureInfo.CurrentUICulture.Name);
            Assert.Equal("Trang chủ", localization["home.title"]);
            Assert.Equal(1, changed);

            localization.Attach(app);
            Assert.Equal(count + 1, merged.Count);
            Assert.True(app.TryGetResource("home.title", null, out var title));
            Assert.Equal("Trang chủ", title);
        }
        finally
        {
            while (merged.Count > count)
                merged.RemoveAt(merged.Count - 1);
            TestServices.EnsureInitialized("en");
        }
    }
}
