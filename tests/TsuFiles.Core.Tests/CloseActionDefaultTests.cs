// Closing the main window keeps TsuFiles in the tray by default (owner's request, 16/11/2026): new installs start
// with Tray, and an upgrade gives it to whoever never answered the question (Ask, the old default). A choice made in
// Settings (Quit, or Tray) stays as it is.

using TsuFiles.Core.Settings;

namespace TsuFiles.Core.Tests;

public class CloseActionDefaultTests
{
    [Fact]
    public void New_settings_keep_running_in_the_tray()
    {
        Assert.Equal(CloseAction.Tray, new AppSettings().CloseAction);
    }

    [Theory]
    [InlineData(CloseAction.Ask, CloseAction.Tray)]
    [InlineData(CloseAction.Quit, CloseAction.Quit)]
    [InlineData(CloseAction.Tray, CloseAction.Tray)]
    public void An_upgrade_changes_only_the_unanswered_question(CloseAction before, CloseAction after)
    {
        var settings = new AppSettings { SchemaVersion = 74, CloseAction = before };

        SettingsMigrations.Apply(settings);

        Assert.Equal(after, settings.CloseAction);
        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
    }

    [Fact]
    public void Ask_chosen_after_the_upgrade_stays()
    {
        var settings = new AppSettings { SchemaVersion = AppSettings.CurrentSchemaVersion, CloseAction = CloseAction.Ask };

        SettingsMigrations.Apply(settings);

        Assert.Equal(CloseAction.Ask, settings.CloseAction);
    }
}
