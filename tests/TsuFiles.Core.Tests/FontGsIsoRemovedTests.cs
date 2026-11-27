using TsuFiles.Core.Formats;
using TsuFiles.Core.Presets;
using TsuFiles.Core.Profiles;
using TsuFiles.Core.Settings;

namespace TsuFiles.Core.Tests;

/// <summary>Settings schema 81 (phiếu TG1 G3b): fonts, Ghostscript (EPS / PostScript) and writing ISO images are gone.</summary>
public class FontGsIsoRemovedTests
{
    [Fact]
    public void Version_80_settings_lose_the_engines_and_the_fonts_field()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "settings.json"),
            """
            { "schemaVersion": 80,
              "enginePriority": ["magick", "Font", "ffmpeg", "ghostscript", "iso", "libreoffice"],
              "engineOwnCopies": { "ghostscript": "C:/gs", "ffmpeg": "/opt/ffmpeg" },
              "notifiedEngineVersions": { "ghostscript": "10.08.0", "ffmpeg": "8.0" },
              "turnedOffFields": ["cad", "Font", "legacy"] }
            """);

        var store = new UserDataStore(dir.Path);
        store.Load();

        Assert.Equal(["magick", "ffmpeg", "libreoffice"], store.Settings.EnginePriority);
        Assert.Equal(["ffmpeg"], store.Settings.EngineOwnCopies.Keys);
        Assert.Equal(["ffmpeg"], store.Settings.NotifiedEngineVersions.Keys);
        Assert.Equal(["cad", "legacy"], store.Settings.TurnedOffFields);
        Assert.Equal(AppSettings.CurrentSchemaVersion, store.Settings.SchemaVersion);
    }

    [Fact]
    public void Built_in_presets_leave_and_the_fonts_and_folders_donuts_go_unless_the_user_put_something_on_them()
    {
        var presets = BuiltInData.CreatePresets();
        presets.Add(new Preset { Id = "to-woff2", Name = "WOFF2", TargetFormat = "woff2" });
        presets.Add(new Preset { Id = "to-eps", Name = "EPS", TargetFormat = "eps" });
        presets.Add(new Preset { Id = "to-iso", NameKey = "preset.iso_image", TargetFormat = "iso" });
        presets.Add(new Preset { Id = "my-woff", Name = "My WOFF", TargetFormat = "woff" });
        var profiles = BuiltInData.CreateProfiles();
        profiles.Single(p => p.Id == "vector").PresetIds.Add("to-eps");
        profiles.Add(new ToolbarProfile { Id = "fonts", NameKey = "profile.fonts", Extensions = ["ttf"], PresetIds = ["to-woff2"], BackPresetIds = ["zip-all"] });
        profiles.Add(new ToolbarProfile { Id = "folders", NameKey = "profile.folders", Extensions = [], PresetIds = ["to-iso"], BackPresetIds = [] });

        Assert.True(SettingsMigrations.ApplyToLibrary(80, presets, profiles));

        Assert.DoesNotContain(presets, p => p.Id is "to-woff2" or "to-eps" or "to-iso");
        Assert.Contains(presets, p => p.Id == "my-woff");
        Assert.DoesNotContain(profiles, p => p.Id is "fonts" or "folders");
        Assert.DoesNotContain("to-eps", profiles.Single(p => p.Id == "vector").PresetIds);
        Assert.False(SettingsMigrations.ApplyToLibrary(81, presets, profiles));
    }

    [Fact]
    public void A_folders_donut_with_the_users_own_preset_stays()
    {
        var presets = BuiltInData.CreatePresets();
        presets.Add(new Preset { Id = "to-iso", NameKey = "preset.iso_image", TargetFormat = "iso" });
        var profiles = BuiltInData.CreateProfiles();
        profiles.Add(new ToolbarProfile { Id = "folders", NameKey = "profile.folders", Extensions = [], PresetIds = ["to-iso", "copy-to-backup"] });

        Assert.True(SettingsMigrations.ApplyToLibrary(80, presets, profiles));

        Assert.Equal(["copy-to-backup"], profiles.Single(p => p.Id == "folders").PresetIds);
    }

    [Fact]
    public void Iso_images_are_read_but_not_written_and_fonts_eps_and_postscript_are_unknown()
    {
        Assert.False(FormatRegistry.Get("iso").Writable);
        Assert.Null(FormatRegistry.FindById("ttf"));
        Assert.Null(FormatRegistry.FindById("eps"));
        Assert.Null(FormatRegistry.FindByExtension("ps"));
        Assert.DoesNotContain(FormatFields.All, f => f.Id == "font");
        Assert.False(BuiltInData.AcceptsFolders(new Preset { TargetFormat = "iso", Archive = { CombineIntoOne = true } }));
        Assert.True(BuiltInData.AcceptsFolders(new Preset { TargetFormat = "zip", Archive = { CombineIntoOne = true } }));
    }
}
