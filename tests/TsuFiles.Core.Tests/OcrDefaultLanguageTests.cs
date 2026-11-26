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

public class OfdRemovedTests
{
    [Fact]
    public void Donuts_forget_ofd_once()
    {
        var presets = BuiltInData.CreatePresets();
        var profiles = BuiltInData.CreateProfiles();
        profiles.Single(p => p.Id == "office").Extensions.Add("ofd");
        profiles.Insert(0, new TsuFiles.Core.Profiles.ToolbarProfile { Id = "invoices", Name = "Invoices", Extensions = ["OFD", "pdf"] });

        Assert.True(SettingsMigrations.ApplyToLibrary(78, presets, profiles));

        Assert.DoesNotContain(profiles.SelectMany(p => p.Extensions), e => e.Equals("ofd", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(["pdf"], profiles[0].Extensions);
        Assert.Null(TsuFiles.Core.Formats.FormatRegistry.FindByExtension("ofd"));
    }
}
