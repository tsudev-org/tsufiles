// TsuFiles: kiểu giao diện "TsuFiles Cú đêm" (ADR-0003). Dùng chung khung điều khiển của Soft (Styles/Soft.axaml), chỉ thay
// bề mặt và màu nhấn. Từ tsudev-standards v5 (phiếu 20261121-STD50) mọi màu lấy từ token chuẩn nhúng lúc build
// (StandardTokens): nền đen tuyền / trắng + chữ + trạng thái của lớp nền chung, tím logo cú của bản sắc `files`
// (#A970FF Tối / #7C3AED Sáng, khoá theo ECOSYSTEM_IDENTITY 4.2.1). Màu nhấn cố định, không theo màu người dùng chọn.

using Avalonia.Controls;
using Avalonia.Media;

namespace TsuFiles.App.Services;

internal static class NightOwlPalette
{
    /// <summary>Màu nhấn trên nền tối: token <c>primary</c> (Tối) của bản sắc files.</summary>
    public static string DarkAccent => StandardTokens.Raw("primary", isDark: true);

    /// <summary>Màu nhấn trên nền sáng: token <c>primary</c> (Sáng), chữ trắng trên nút đạt WCAG AA.</summary>
    public static string LightAccent => StandardTokens.Raw("primary", isDark: false);

    /// <summary>Nền trang tối: token <c>bg-base</c> (đen tuyền #0A0A0A).</summary>
    public static string DarkPage => StandardTokens.Raw("bg-base", isDark: true);

    /// <summary>Nền trang sáng: token <c>bg-base</c>.</summary>
    public static string LightPage => StandardTokens.Raw("bg-base", isDark: false);

    // Khóa tài nguyên Avalonia -> vai trò token (DESIGN_SYSTEM 1.1). Viền nút là ranh giới vùng tương tác nên dùng
    // `border-control` (>= 3:1), không dùng `border` trang trí.
    private static readonly (string Key, string Role)[] Surfaces =
    [
        ("PageBrush", "bg-base"),
        ("SurfaceBrush", "bg-surface"),
        ("SurfaceAltBrush", "bg-subtle"),
        ("SubtleBorderBrush", "border"),
        ("TextPrimaryBrush", "text-primary"),
        ("TextSecondaryBrush", "text-secondary"),
        ("DangerBrush", "danger"),
        ("SuccessBrush", "success"),
        ("WarningBrush", "warning"),
        ("ButtonBackground", "bg-surface"),
        ("ButtonBackgroundPointerOver", "bg-hover"),
        ("ButtonBackgroundPressed", "border"),
        ("ButtonBorderBrush", "border-control"),
        ("ButtonBorderBrushPointerOver", "border-control"),
        ("ButtonBorderBrushPressed", "border-control"),
    ];

    /// <summary>Màu nhấn của một biến thể.</summary>
    public static Color Accent(bool isDark) => StandardTokens.Color("primary", isDark);

    /// <summary>Ghi bề mặt + màu nhấn Cú đêm của một biến thể (sáng/tối) vào <paramref name="dictionary"/>.</summary>
    public static void Apply(IResourceDictionary dictionary, bool isDark)
    {
        foreach (var (key, role) in Surfaces)
            dictionary[key] = new SolidColorBrush(StandardTokens.Color(role, isDark));
        dictionary["DangerFaintBrush"] = new SolidColorBrush(StandardTokens.Color("danger", isDark), isDark ? 0.15 : 0.10);
        var accent = Accent(isDark);
        SoftPalette.ApplyAccent(dictionary, accent);
        // Chữ "màu nhấn đậm" (thẻ đang chọn, nhãn): token `accent` - đạt 4.5:1 trên cả 4 nền ở hai chế độ.
        dictionary["AccentStrongBrush"] = new SolidColorBrush(StandardTokens.Color("accent", isDark));
    }
}
