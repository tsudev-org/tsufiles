// TsuFiles: token chuẩn nhúng lúc build (tsudev-standards v5, phiếu 20261121-STD50) - đúng bản sắc `files`, gộp đè đúng
// lớp nền, và mọi cặp màu Cú đêm mà giao diện thật sinh ra đạt ngưỡng DESIGN_SYSTEM 1.1 ở cả Sáng và Tối.

using System.Text.Json;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using TsuFiles.App.Services;

namespace TsuFiles.App.Tests;

public class StandardTokensTests
{
    private static string RepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TsuFiles.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine([dir!.FullName, .. parts]);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Embedded_tokens_are_the_synced_files_product_over_the_shared_base(bool isDark)
    {
        var mode = isDark ? "dark" : "light";
        using var product = JsonDocument.Parse(File.ReadAllText(RepoFile(".standards", "tokens", "products", "files.json")));
        using var baseDoc = JsonDocument.Parse(File.ReadAllText(RepoFile(".standards", "tokens", "design-tokens.json")));
        Assert.Equal("files", product.RootElement.GetProperty("id").GetString());

        foreach (var p in product.RootElement.GetProperty("colors").GetProperty(mode).EnumerateObject())
            Assert.Equal(p.Value.GetString(), StandardTokens.Raw(p.Name, isDark));
        // Vai trò nền/chữ/trạng thái không đổi theo sản phẩm.
        foreach (var role in new[] { "bg-base", "bg-surface", "text-primary", "border-control", "danger", "success" })
            Assert.Equal(baseDoc.RootElement.GetProperty("color").GetProperty(mode).GetProperty(role).GetString(),
                StandardTokens.Raw(role, isDark));
    }

    [AvaloniaFact]
    public void Owl_purple_and_mem_shape_are_what_the_standard_locked()
    {
        Assert.Equal("#A970FF", NightOwlPalette.DarkAccent);
        Assert.Equal("#7C3AED", NightOwlPalette.LightAccent);
        Assert.Equal("#0A0A0A", NightOwlPalette.DarkPage);
        Assert.Equal(10, StandardTokens.Radius("control"));
        Assert.Equal(14, StandardTokens.Radius("lg"));
        Assert.Equal("Lexend", StandardTokens.DisplayFont);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Night_owl_pairs_meet_the_design_system_thresholds(bool isDark)
    {
        Color T(string role) => StandardTokens.Color(role, isDark);
        string[] fills = ["bg-base", "bg-surface", "bg-subtle"];
        foreach (var bg in fills)
        {
            Assert.True(SoftPalette.ContrastRatio(T("text-primary"), T(bg)) >= 10, $"text-primary/{bg}");
            foreach (var fg in new[] { "text-secondary", "text-muted", "text-link", "accent" })
                Assert.True(SoftPalette.ContrastRatio(T(fg), T(bg)) >= 4.5, $"{fg}/{bg}");
            foreach (var fg in new[] { "border-control", "focus-ring", "icon-info", "icon-nav", "primary" })
                Assert.True(SoftPalette.ContrastRatio(T(fg), T(bg)) >= 3, $"{fg}/{bg}");
        }
        // Nhãn trên nút nhấn: app tự chọn trắng/mực đen (SoftPalette.OnAccent) - phải trùng token on-primary và đạt AA.
        var primary = T("primary");
        var label = SoftPalette.OnAccent(primary);
        Assert.Equal(T("on-primary") == Colors.White, label == Colors.White);
        Assert.True(SoftPalette.ContrastRatio(label, primary) >= 4.5);
    }

    // UI1 (DESIGN_SYSTEM 1.1, v4.1.0): biểu tượng + chữ của mục đang chọn ở thanh điều hướng (Styles/Soft.axaml,
    // ListBox.nav :selected) = AccentStrongBrush trên viên AccentSoftBrush (primary 18%) phủ lên nền thật; hover = nền
    // bg-hover. Đo trên MỌI nền có thể nằm dưới thanh điều hướng, cả Sáng và Tối.
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Selected_nav_icon_reads_on_its_indicator_pill(bool isDark)
    {
        var dict = new Avalonia.Controls.ResourceDictionary();
        NightOwlPalette.Apply(dict, isDark);
        var icon = Assert.IsType<SolidColorBrush>(dict["AccentStrongBrush"]).Color;
        var pill = Assert.IsType<SolidColorBrush>(dict["AccentSoftBrush"]);
        foreach (var role in new[] { "bg-base", "bg-surface", "bg-subtle" })
        {
            var under = StandardTokens.Color(role, isDark);
            var a = pill.Opacity * pill.Color.A / 255.0;
            var onPill = Color.FromRgb(
                (byte)Math.Round(pill.Color.R * a + under.R * (1 - a)),
                (byte)Math.Round(pill.Color.G * a + under.G * (1 - a)),
                (byte)Math.Round(pill.Color.B * a + under.B * (1 - a)));
            Assert.True(SoftPalette.ContrastRatio(icon, onPill) >= 4.5, $"chọn trên {role}: {SoftPalette.ContrastRatio(icon, onPill):0.00}");
            Assert.True(SoftPalette.ContrastRatio(StandardTokens.Color("icon-nav", isDark), under) >= 3, $"icon-nav/{role}");
        }
        Assert.True(SoftPalette.ContrastRatio(StandardTokens.Color("text-secondary", isDark), StandardTokens.Color("bg-hover", isDark)) >= 4.5);
    }

    [AvaloniaTheory]
    [InlineData("#7C3AED", 255, 0x7C, 0x3A, 0xED)]
    [InlineData("rgba(169, 112, 255, 0.16)", 41, 169, 112, 255)]
    public void Parses_hex_and_rgba(string value, byte a, byte r, byte g, byte b) =>
        Assert.Equal(Color.FromArgb(a, r, g, b), StandardTokens.Parse(value));
}
