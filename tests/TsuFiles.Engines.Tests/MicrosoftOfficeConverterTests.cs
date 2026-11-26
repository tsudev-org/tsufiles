// The Microsoft Office engine (Windows, where Office is installed): what it offers and that it wins over LibreOffice
// and the built-in route for Office files. Converting needs Office itself, measured by hand against Word and Excel
// (phiếu TG1); these tests pretend which Office programs are installed.

using TsuFiles.Core.Conversion;
using TsuFiles.Engines.Office;

namespace TsuFiles.Engines.Tests;

public class MicrosoftOfficeConverterTests(EngineFixture fx) : IClassFixture<EngineFixture>
{
    [Fact]
    public void Office_files_go_to_pdf_and_old_formats_to_new_ones_only_on_windows_with_office()
    {
        var all = new MicrosoftOfficeConverter(_ => true);
        var wordOnly = new MicrosoftOfficeConverter(progId => progId == "Word.Application");
        var none = new MicrosoftOfficeConverter(_ => false);

        Assert.Empty(none.Edges);
        Assert.False(none.GetStatus().IsAvailable);
        if (!OperatingSystem.IsWindows())
        {
            Assert.Empty(all.Edges);
            Assert.Equal("engine.reason.unsupported_platform", all.GetStatus().ReasonKey);
            return;
        }

        Assert.True(all.GetStatus().IsAvailable);
        foreach (var (from, to) in new[] { ("docx", "pdf"), ("doc", "docx"), ("rtf", "pdf"), ("xlsx", "pdf"), ("xls", "xlsx"), ("pptx", "pdf"), ("ppt", "pptx") })
            Assert.Contains(all.Edges, e => e.From == from && e.To == to && e.Cost == 0);
        Assert.DoesNotContain(all.Edges, e => e.From == e.To);
        Assert.DoesNotContain(all.Edges, e => e.From == "pdf");
        Assert.All(wordOnly.Edges, e => Assert.Contains(e.From, new[] { "docx", "doc", "docm", "dotx", "dot", "rtf", "odt" }));
        Assert.Equal("Word → PDF", wordOnly.GetStatus().Detail);
    }

    [Fact]
    public void Office_wins_over_libreoffice_and_the_built_in_route_where_it_is_installed()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Microsoft Office runs on Windows only.");
        var office = new MicrosoftOfficeConverter(_ => true);
        // Every engine at the bottom of the list (the most a priority adds), LibreOffice included as if installed.
        var planner = new RoutePlanner(fx.Converters.Where(c => c.Id != office.Id).Append(office), _ => 9);

        foreach (var (from, to) in new[] { ("docx", "pdf"), ("xlsx", "pdf"), ("pptx", "pdf"), ("doc", "docx") })
            Assert.Equal("msoffice", Assert.Single(planner.Plan(from, to)!.Steps).Converter.Id);
    }
}
