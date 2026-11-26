// The embedded fonts are read family by family when text asks (issue #957), and which families were read before never
// changes the font a letter ends up in: SoftPalette's chain decides (#1095).

using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Platform;
using TsuFiles.App.Services;

namespace TsuFiles.App.Tests;

public class TsuFilesFontCollectionTests
{
    private const string Key = "fonts:TsuFilesOnDemand";

    /// <summary>The embedded families: only Noto Sans since phiếu TG1 (Vietnamese and English UI).</summary>
    private static readonly string[] Families = ["Noto Sans"];

    private static void RequireFonts()
    {
        TestServices.EnsureInitialized("en");
        Assert.SkipUnless(AssetLoader.GetAssets(TsuFilesFontCollection.Source, null).Any(), "Requires build/fetch-fonts.ps1.");
    }

    /// <summary>A new collection in the font manager (it replaces and disposes the last, or the fonts would add up).</summary>
    private static TsuFilesFontCollection NewCollection()
    {
        var fonts = new TsuFilesFontCollection(Key);
        FontManager.Current.AddFontCollection(fonts);
        return fonts;
    }

    private static string? Match(string language, int letter, FontWeight weight = FontWeight.Normal)
    {
        var chain = ThemeService.FontFamilies(language).Replace("fonts:Filee", Key, StringComparison.Ordinal);
        return FontManager.Current.TryMatchCharacter(letter, FontStyle.Normal, weight, FontStretch.Normal, FontFamily.Parse(chain), null, out var typeface)
            ? typeface.GlyphTypeface.FamilyName
            : null;
    }

    [Theory]
    [InlineData("Noto Sans", "NotoSans-Regular.ttf", true)]
    [InlineData("Noto Sans", "NotoSans-Bold.ttf", true)]
    [InlineData("Noto Sans", "NotoSansKR-Regular.otf", false)]
    [InlineData("Noto Sans KR", "NotoSansKR-Bold.otf", true)]
    [InlineData("Noto Sans KR", "NotoSansJP-Bold.otf", false)]
    [InlineData("Segoe UI", "NotoSans-Regular.ttf", false)]
    public void A_font_file_belongs_to_its_family(string family, string file, bool expected) =>
        Assert.Equal(expected, TsuFilesFontCollection.IsFileOf(family, file));

    [AvaloniaFact]
    public void A_family_is_read_when_asked_for_and_not_before()
    {
        RequireFonts();
        using var fonts = new TsuFilesFontCollection(Key);
        Assert.Empty(fonts);

        Assert.True(fonts.TryGetGlyphTypeface("Noto Sans", FontStyle.Normal, FontWeight.Bold, FontStretch.Normal, out var bold));
        Assert.Equal(FontWeight.Bold, bold.Weight);
        // The fonts of other scripts are no longer embedded (phiếu TG1): the system's are used.
        Assert.False(fonts.TryGetGlyphTypeface("Noto Sans KR", FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, out _));

        Assert.False(fonts.TryGetGlyphTypeface("Not a font", FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, out _));
    }

    [AvaloniaFact]
    public void A_letter_is_only_matched_in_the_family_asked_for()
    {
        RequireFonts();
        using var fonts = new TsuFilesFontCollection(Key);
        Assert.False(fonts.TryMatchCharacter('한', FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, "Noto Sans", null, out _));
        Assert.True(fonts.TryMatchCharacter('ữ', FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, "Noto Sans", null, out _));
    }

    [AvaloniaTheory]
    [InlineData("vi")]
    [InlineData("en")]
    public void Vietnamese_letters_are_drawn_in_the_embedded_noto_sans(string language)
    {
        RequireFonts();
        NewCollection();
        foreach (var letter in "AĐăâêôơưẠếữỡỹ")
            Assert.Equal("Noto Sans", Match(language, letter));
    }

    [AvaloniaTheory]
    [InlineData("en")]
    [InlineData("vi")]
    public void A_letter_takes_the_same_font_whatever_was_read_before(string language)
    {
        RequireFonts();
        // The first letter a new collection sees is the hard case, so each letter gets a new one.
        foreach (var letter in "Aữ한あ将→")
        {
            foreach (var weight in new[] { FontWeight.Normal, FontWeight.Bold })
            {
                NewCollection();
                var firstLetter = Match(language, letter, weight);

                var fonts = NewCollection();
                foreach (var family in Families)
                    fonts.TryGetGlyphTypeface(family, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, out _);
                var allRead = Match(language, letter, weight);

                Assert.True(firstLetter == allRead, $"{letter} {weight} in {language}: {firstLetter} when first, {allRead} with all read");
            }
        }
    }
}
