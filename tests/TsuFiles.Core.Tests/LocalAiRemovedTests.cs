using TsuFiles.Core.Presets;
using TsuFiles.Core.Profiles;
using TsuFiles.Core.Settings;

namespace TsuFiles.Core.Tests;

/// <summary>Settings schema 80 (phiếu TG1 G3b): the local AI tools (Whisper, Upscayl, Ollama translation) are gone.</summary>
public class LocalAiRemovedTests
{
    [Fact]
    public void Version_79_settings_lose_the_local_AI_engines()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "settings.json"),
            """
            { "schemaVersion": 79,
              "enginePriority": ["magick", "Whisper", "ffmpeg", "upscaler", "translator", "libreoffice"],
              "engineOwnCopies": { "whisper": "C:/whisper", "ffmpeg": "/opt/ffmpeg" },
              "notifiedEngineVersions": { "upscaler": "20251207", "ffmpeg": "8.0" },
              "preferredOllamaModel": "llama3.2" }
            """);

        var store = new UserDataStore(dir.Path);
        store.Load();

        Assert.Equal(["magick", "ffmpeg", "libreoffice"], store.Settings.EnginePriority);
        Assert.Equal(["ffmpeg"], store.Settings.EngineOwnCopies.Keys);
        Assert.Equal(["ffmpeg"], store.Settings.NotifiedEngineVersions.Keys);
        Assert.Equal(AppSettings.CurrentSchemaVersion, store.Settings.SchemaVersion);
    }

    [Fact]
    public void Built_in_AI_presets_leave_the_library_and_every_donut_the_users_own_stay()
    {
        var presets = BuiltInData.CreatePresets();
        presets.Add(new Preset { Id = "translate", TargetFormat = BuiltInData.SameAsSource });
        presets.Add(new Preset { Id = "caption-video", TargetFormat = "srt" });
        presets.Add(new Preset { Id = "tool-upscale", TargetFormat = BuiltInData.SameAsSource, Kind = PresetKind.Tool, Tool = "upscale" });
        presets.Add(new Preset { Id = "my-subtitles", Name = "My subtitles", TargetFormat = "srt" });
        var profiles = BuiltInData.CreateProfiles();
        profiles.Single(p => p.Id == "video").PresetIds.Add("caption-video");
        profiles.Single(p => p.Id == "office").BackPresetIds!.Insert(0, "translate");
        profiles.Insert(0, new ToolbarProfile { Id = "mine", Name = "Mine", PresetIds = ["tool-upscale", "my-subtitles"] });

        Assert.True(SettingsMigrations.ApplyToLibrary(79, presets, profiles));

        Assert.DoesNotContain(presets, p => p.Id is "translate" or "caption-video" or "tool-upscale");
        Assert.Contains(presets, p => p.Id == "my-subtitles");
        Assert.DoesNotContain(profiles.SelectMany(p => p.PresetIds.Concat(p.BackPresetIds ?? [])),
            id => id is "translate" or "caption-video" or "tool-upscale");
        Assert.Equal(["my-subtitles"], profiles[0].PresetIds);
        Assert.False(SettingsMigrations.ApplyToLibrary(80, presets, profiles));
    }

    [Fact]
    public void A_preset_file_with_the_old_AI_options_still_loads()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "settings.json"), """{ "schemaVersion": 79 }""");
        File.WriteAllText(Path.Combine(dir.Path, "presets.json"),
            """
            [ { "id": "my-video", "name": "My video", "targetFormat": "srt",
                "media": { "caption": true, "spokenLanguage": "vi" },
                "translate": { "enabled": true, "targetLanguage": "en", "model": "llama3.2" },
                "upscale": { "enabled": true, "scale": 4 } } ]
            """);
        File.WriteAllText(Path.Combine(dir.Path, "profiles.json"), """[ { "id": "mine", "name": "Mine", "presetIds": ["my-video"] } ]""");

        var store = new UserDataStore(dir.Path);
        store.Load();

        Assert.Equal("srt", store.Presets.Single(p => p.Id == "my-video").TargetFormat);
    }
}
