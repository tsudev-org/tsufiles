using TsuFiles.Core.Presets;
using TsuFiles.Core.Settings;

namespace TsuFiles.Core.Tests;

public class OcrDefaultLanguageTests
{
    private static Preset Saved(string id, bool ocr, string language) =>
        new() { Id = id, TargetFormat = "pdf", Pdf = { Ocr = ocr, OcrLanguage = language } };

    [Fact]
    public void A_new_preset_reads_Vietnamese() => Assert.Equal("vie", new Preset().Pdf.OcrLanguage);

    [Fact]
    public void The_old_default_becomes_Vietnamese_only_where_nobody_chose_it()
    {
        var builtIn = BuiltInData.CreatePresets().Single(p => p.Id == "searchable-pdf").Id;
        List<Preset> presets =
        [
            Saved(builtIn, ocr: true, "eng"),
            Saved("own-off", ocr: false, "eng"),
            Saved("own-on", ocr: true, "eng"),
            Saved("own-mixed", ocr: false, "deu+eng"),
        ];

        Assert.True(SettingsMigrations.ApplyToLibrary(77, presets, BuiltInData.CreateProfiles()));

        string Language(string id) => presets.Single(p => p.Id == id).Pdf.OcrLanguage;
        Assert.Equal("vie", Language(builtIn));
        Assert.Equal("vie", Language("own-off"));
        Assert.Equal("eng", Language("own-on"));
        Assert.Equal("deu+eng", Language("own-mixed"));
    }
}
